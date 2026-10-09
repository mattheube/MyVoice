namespace MyVoice.Core;
public record AudioDevice(string Id, string Name);
public record AudioLevel(double PeakDb, double AverageDb, bool Clipping, long Frames);
public record CalibrationResult(bool CanApply, double GainDb, double NoiseDb, double VoiceDb, string Reason);
public interface IAudioInputService : IDisposable
{
    IReadOnlyList<AudioDevice> GetDevices(); void Start(string id); void Stop();
}
public interface IMicrophoneService : IAudioInputService
{
    bool IsRunning
    {
        get;
    }
    bool Muted
    {
        get; set;
    }
    double GainDb
    {
        get; set;
    }
    AudioLevel Level
    {
        get;
    }
    event Action<string>? Faulted; event Action? DevicesChanged; Task<CalibrationResult> CalibrateAsync(CancellationToken token);
}
public interface IAudioOutputService : IDisposable
{
    IReadOnlyList<AudioDevice> GetDevices(); void Start(string id, int sampleRate, int channels); void Write(ReadOnlySpan<float> samples); void Stop();
}
public interface IAudioProcessingService
{
    void Process(Span<float> samples);
}
public interface IVirtualAudioDevice : IAudioOutputService
{
    bool IsConfigured
    {
        get;
    }
}
public interface ISoundboardService
{
    Task<Guid> ImportAsync(string file, CancellationToken token); void Play(Guid id); void Pause(Guid id); void StopAll();
}
public interface IVoiceEffectService : IAudioProcessingService
{
    string Preset
    {
        get; set;
    }
}
public interface IVoiceConversionBackend : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken token); Task LoadModelAsync(string path, CancellationToken token); Task UnloadModelAsync(CancellationToken token); Task StartAsync(CancellationToken token); ValueTask<int> ProcessAudioChunkAsync(ReadOnlyMemory<float> input, Memory<float> output, CancellationToken token); Task StopAsync(CancellationToken token);
}
public sealed partial class AppSettings
{
    public int ConfigVersion { get; set; } = 1;
    public string? InputDeviceId
    {
        get; set;
    }
    public double GainDb
    {
        get; set;
    }
    public bool Muted
    {
        get; set;
    }
    public bool CloseToTray { get; set; } = true;
    public bool StartMinimized
    {
        get; set;
    }
    public bool LaunchAtStartup {get;set;} = true;
    public bool Animations { get; set; } = true;
    public string Language { get; set; } = "fr";
    public uint HotkeyModifiers
    {
        get; set;
    }
    public uint HotkeyKey
    {
        get; set;
    }
    public void Validate()
    {
        if (ConfigVersion != 1)
            throw new InvalidDataException("Unsupported configuration version");
        GainDb = double.IsFinite(GainDb) ? Math.Clamp(GainDb, -20, 30) : 0;
        Language = Language == "en" ? "en" : "fr";
        ValidateStudio();
    }
}
public static class Calibration
{
    public static CalibrationResult Analyze(IReadOnlyList<double> rms, double peak)
    {
        if (rms.Count < 20)
            return new(false, 0, -90, -90, "insufficient");
        var sorted = rms.Order().ToArray();
        var noise = Db(sorted[(int)(sorted.Length * .15)]);
        var voice = Db(sorted[(int)(sorted.Length * .8)]);
        if (peak >= .995)
            return new(false, 0, noise, voice, "clipping");
        if (voice < -55 || voice - noise < 8)
            return new(false, 0, noise, voice, "noise");
        var gain = Math.Clamp(Math.Min(-20 - voice, -3 - Db(peak)), -20, 18);
        return new(true, Math.Round(gain, 1), noise, voice, "ready");
    }
    public static double Db(double value) => 20 * Math.Log10(Math.Max(value, 0.0000316227766));
}
public sealed class GainProcessor : IAudioProcessingService
{
    private double gain; private int mute;
    public double GainDb
    {
        get => Volatile.Read(ref gain); set => Volatile.Write(ref gain, Math.Clamp(double.IsFinite(value) ? value : 0, -20, 30));
    }
    public bool Muted
    {
        get => Volatile.Read(ref mute) != 0; set => Volatile.Write(ref mute, value ? 1 : 0);
    }
    public void Process(Span<float> samples)
    {
        var factor = Muted ? 0 : (float)Math.Pow(10, GainDb / 20);
        for (int i = 0; i < samples.Length; i++)
            samples[i] = float.IsFinite(samples[i]) ? Math.Clamp(samples[i] * factor, -.98f, .98f) : 0;
    }
}
