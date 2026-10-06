using System.Runtime.InteropServices;
using MyVoice.Core;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
namespace MyVoice.Audio;
/// <summary>WASAPI capture. No audio is written to disk. UI polls immutable level snapshots.</summary>
public sealed class MicrophoneService : IMicrophoneService
{
    private readonly MMDeviceEnumerator notifications = new();
    private readonly DeviceNotifications callback;
    public MicrophoneService()
    {
        callback = new DeviceNotifications(() => DevicesChanged?.Invoke());
        notifications.RegisterEndpointNotificationCallback(callback);
    }
    private WasapiCapture? capture;
    private MMDevice? device;
    private readonly GainProcessor processor = new();
    private AudioLevel level = new(-90, -90, false, 0);
    private volatile bool running;
    private readonly object calibrationGate = new();
    private List<double>? calibration;
    private double calibrationPeak;
    private long frames;
    private readonly float[] mono = new float[65536];
    private long sourcePosition; private double nextPosition; private float previousSample;
    public event AudioSamplesHandler? Samples;
    public bool IsRunning => running;
    public bool Muted
    {
        get => processor.Muted; set => processor.Muted = value;
    }
    public double GainDb
    {
        get => processor.GainDb; set => processor.GainDb = value;
    }
    public AudioLevel Level => Volatile.Read(ref level);
    public event Action<string>? Faulted;
    public event Action? DevicesChanged;
    public IReadOnlyList<AudioDevice> GetDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioDevice>();
        foreach (var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            using (endpoint)
                result.Add(new(endpoint.ID, endpoint.FriendlyName));
        try
        {
            using var preferred = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return result.OrderByDescending(d => d.Id == preferred.ID).ToArray();
        }
        catch (COMException) { return result; }
    }
    public void Start(string id)
    {
        Stop();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            device = enumerator.GetDevice(id);
            capture = new WasapiCapture(device, false, 20);
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(device.AudioClient.MixFormat.SampleRate, device.AudioClient.MixFormat.Channels);
            capture.DataAvailable += OnData;
            capture.RecordingStopped += OnStopped;
            frames = 0;
            sourcePosition = 0;
            nextPosition = 0;
            previousSample = 0;
            running = true;
            capture.StartRecording();
        }
        catch { Stop(); throw; }
    }
    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (!running || e.BytesRecorded == 0)
            return;
        var samples = MemoryMarshal.Cast<byte, float>(e.Buffer.AsSpan(0, e.BytesRecorded));
        double sum = 0, peak = 0;
        foreach (var sample in samples)
        {
            var v = float.IsFinite(sample) ? sample : 0;
            sum += (double)v * v;
            peak = Math.Max(peak, Math.Abs(v));
        }
        var rms = Math.Sqrt(sum / samples.Length);
        lock (calibrationGate)
        {
            if (calibration != null && calibration.Count < 2000)
            {
                calibration.Add(rms);
                calibrationPeak = Math.Max(calibrationPeak, peak);
            }
        }
        var boostedPeak = peak * Math.Pow(10, GainDb / 20);
        var factor = Muted ? 0 : (float)Math.Pow(10, GainDb / 20);
        for (int i = 0; i < samples.Length; i++)
            samples[i] = float.IsFinite(samples[i]) ? samples[i] * factor : 0;
        int channels = capture?.WaveFormat.Channels ?? 1;
        int sampleRate = capture?.WaveFormat.SampleRate ?? 48000;
        int written = 0;
        for (int i = 0; i + channels <= samples.Length; i += channels)
        {
            float current = 0;
            for (int c = 0; c < channels; c++)
                current += samples[i + c] / channels;
            while (nextPosition <= sourcePosition)
            {
                float fraction = (float)(nextPosition - (sourcePosition - 1));
                mono[written++] = previousSample + (current - previousSample) * Math.Clamp(fraction, 0, 1);
                if (written == mono.Length)
                {
                    Samples?.Invoke(mono);
                    written = 0;
                }
                nextPosition += (double)sampleRate / 48000;
            }
            previousSample = current;
            sourcePosition++;
        }
        if (written > 0)
            Samples?.Invoke(mono.AsSpan(0, written));
        double outputSum = 0, outputPeak = 0;
        foreach (var sample in samples)
        {
            outputSum += (double)sample * sample;
            outputPeak = Math.Max(outputPeak, Math.Abs(sample));
        }
        frames += samples.Length / (capture?.WaveFormat.Channels ?? 1);
        Volatile.Write(ref level, new(Calibration.Db(outputPeak), Calibration.Db(Math.Sqrt(outputSum / samples.Length)), peak >= .995 || (!Muted && boostedPeak >= .98), frames));
    }
    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        running = false;
        Volatile.Write(ref level, new(-90, -90, false, frames));
        if (e.Exception != null)
            Faulted?.Invoke(e.Exception.Message);
    }
    public async Task<CalibrationResult> CalibrateAsync(CancellationToken token)
    {
        if (!running)
            throw new InvalidOperationException("Microphone is not running");
        lock (calibrationGate)
        {
            if (calibration != null)
                throw new InvalidOperationException("Calibration already running");
            calibration = new(600);
            calibrationPeak = 0;
        }
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), token);
            lock (calibrationGate)
            {
                if (!running)
                    return new(false, 0, -90, -90, "insufficient");
                return Calibration.Analyze(calibration!, calibrationPeak);
            }
        }
        finally { lock (calibrationGate) calibration = null; }
    }
    public void Stop()
    {
        running = false;
        var old = capture;
        capture = null;
        if (old != null)
        {
            old.DataAvailable -= OnData;
            old.RecordingStopped -= OnStopped;
            try
            {
                old.StopRecording();
            }
            finally { old.Dispose(); }
        }
        device?.Dispose();
        device = null;
        Volatile.Write(ref level, new(-90, -90, false, frames));
    }
    public void Dispose()
    {
        notifications.UnregisterEndpointNotificationCallback(callback);
        try
        {
            Stop();
        }
        finally { notifications.Dispose(); }
    }
    private sealed class DeviceNotifications(Action changed) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string id, DeviceState state) => changed();
        public void OnDeviceAdded(string id) => changed();
        public void OnDeviceRemoved(string id) => changed();
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string id) => changed();
        public void OnPropertyValueChanged(string id, PropertyKey key) => changed();
    }
}
