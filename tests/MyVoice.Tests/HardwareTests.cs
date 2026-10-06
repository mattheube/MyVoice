using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.Infrastructure;
using MyVoice.Soundboard;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Runtime.InteropServices;
public static class HardwareTests
{
    public static async Task Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyVoice-OutputTest-" + Guid.NewGuid().ToString("N"));
        new LocalStore(root);
        using var sounds = new SoundboardService(root);
        using var graph = new AudioGraph(sounds);
        var outputs = graph.Devices();
        foreach (var d in outputs)
            Console.WriteLine("OUTPUT " + d.Name);
        var chosen = outputs.FirstOrDefault(d => d.Name.Contains("Razer", StringComparison.OrdinalIgnoreCase));
        if (chosen == null)
        {
            Console.WriteLine("SKIP no Razer output");
            return;
        }
        string? error = null;
        graph.Error += e => error = e;
        graph.Processor.Settings = new ProcessingSettings { Gate = false, NoiseSuppression = 0, Compressor = false };
        graph.HearMyself = true;
        graph.MonitorVolume = .5f;
        using var enumerator = new MMDeviceEnumerator();
        using var endpoint = enumerator.GetDevice(chosen.Id);
        using var loopback = new WasapiLoopbackCapture(endpoint);
        long bytes = 0;
        double peak = 0;
        loopback.DataAvailable += (_, e) => { bytes += e.BytesRecorded; if (loopback.WaveFormat.BitsPerSample == 32) { foreach (var x in MemoryMarshal.Cast<byte, float>(e.Buffer.AsSpan(0, e.BytesRecorded))) if (float.IsFinite(x)) peak = Math.Max(peak, Math.Abs(x)); } };
        loopback.StartRecording();
        graph.Configure(chosen.Id, null);
        var block = new float[480];
        for (int frame = 0; frame < 60; frame++)
        {
            for (int i = 0; i < 480; i++)
                block[i] = (float)(.01 * Math.Sin(2 * Math.PI * 733 * (frame * 480 + i) / 48000));
            graph.PushMicrophone(block);
            await Task.Delay(10);
        }
        await Task.Delay(150);
        loopback.StopRecording();
        graph.Configure(null, null);
        if (error != null || bytes == 0 || peak < .00001)
            throw new Exception($"Output validation failed: {error}, bytes={bytes}, peak={peak}");
        Console.WriteLine($"PASS real WASAPI headphone playback and loopback: {bytes} bytes, peak {Calibration.Db(peak):0.0} dB");
    }
}
