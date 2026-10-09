using MyVoice.Core;
using MyVoice.Infrastructure;
using MyVoice.Audio;
int passed = 0;
void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
void Assert(bool condition, string message = "assertion failed") { if (!condition) throw new Exception(message); }
Test("Gain scales real samples", () => { var dsp = new GainProcessor { GainDb = 6.020599913 }; float[] data = [.1f, -.1f]; dsp.Process(data); Assert(Math.Abs(data[0] - .2) < .00001 && Math.Abs(data[1] + .2) < .00001); });
Test("Mute zeros physical mic samples", () => { var dsp = new GainProcessor { Muted = true, GainDb = 30 }; float[] data = [.1f, -.5f, 1f]; dsp.Process(data); Assert(data.All(x => x == 0)); });
Test("Peak protection bounds boosted audio", () => { var dsp = new GainProcessor { GainDb = 30 }; float[] data = [1, -1]; dsp.Process(data); Assert(data[0] == .98f && data[1] == -.98f); });
Test("Restore unity gain", () => { var dsp = new GainProcessor { GainDb = 0 }; float[] data = [.1f]; dsp.Process(data); Assert(data[0] == .1f); });
Test("Gain sanitizes invalid settings", () => { var dsp = new GainProcessor { GainDb = double.NaN }; Assert(dsp.GainDb == 0); dsp.GainDb = 100; Assert(dsp.GainDb == 30); });
Test("Calibration recommends safe gain", () => { var levels = Enumerable.Repeat(.001, 30).Concat(Enumerable.Repeat(.03, 70)).ToArray(); var r = Calibration.Analyze(levels, .1); Assert(r.CanApply && r.GainDb > 0 && r.GainDb <= 17); });
Test("Calibration rejects noisy constant input", () => { Assert(!Calibration.Analyze(Enumerable.Repeat(.02, 100).ToArray(), .1).CanApply); });
Test("Calibration rejects clipping", () => { Assert(Calibration.Analyze(Enumerable.Repeat(.1, 100).ToArray(), 1).Reason == "clipping"); });
Test("Calibration rejects silence", () => { Assert(!Calibration.Analyze(new double[100], 0).CanApply); });
Test("Calibration caps distant microphone gain", () => { var levels = Enumerable.Repeat(.0001, 30).Concat(Enumerable.Repeat(.003, 70)).ToArray(); Assert(Calibration.Analyze(levels, .01).GainDb <= 18); });
Test("Calibration rejects missing audio", () => { Assert(!Calibration.Analyze(new double[2], 0).CanApply); });
Test("Non-finite audio is silenced", () => { var dsp = new GainProcessor(); float[] data = [float.NaN, float.PositiveInfinity]; dsp.Process(data); Assert(data.All(x => x == 0)); });
var folder = Path.Combine(Path.GetTempPath(), "MyVoice-Tests-" + Guid.NewGuid().ToString("N"));
Test("Settings round trip", () => { var store = new LocalStore(folder); store.Save(new() { GainDb = 7.5, InputDeviceId = "device", Muted = true }); var s = store.Load(); Assert(s.GainDb == 7.5 && s.InputDeviceId == "device" && s.Muted); });
Test("Corrupt primary recovers backup", () => { var store = new LocalStore(folder); store.Save(new() { GainDb = 12 }); File.WriteAllText(Path.Combine(folder, "config", "config.json"), "broken"); Assert(store.Load().GainDb == 7.5 && store.RecoveryMessage == "backup"); });
Test("Recovery does not replace good backup with corruption", () => { var store = new LocalStore(folder); store.Save(store.Load()); Assert(new LocalStore(folder).Load().GainDb == 7.5); });
Test("Unknown config version falls back", () => { var store = new LocalStore(folder); File.WriteAllText(Path.Combine(folder, "config", "config.json"), "{\"ConfigVersion\":999}"); Assert(store.Load().ConfigVersion == 1); });
Console.WriteLine($"{passed} deterministic tests passed.");
await StudioTests.Run();
V2Tests.Run();
QualityTests.Run();
StreamingQueueTests.Run();
await UpdateTransportTests.Run();
if(args.Contains("--expressive"))await ExpressiveTests.Run(args.Contains("--cpu"),args.Contains("--style-only"));
if(args.Contains("--virtual"))await VirtualRoutingTests.Run();
if (args.Contains("--ai")) await AiIntegrationTests.Run(args.Contains("--cpu") ? "cpu" : "auto");
if (args.Contains("--routing")) await HardwareTests.Run();
if (args.Contains("--hardware"))
{
    using var mic = new MicrophoneService();
    var devices = mic.GetDevices();
    Console.WriteLine($"Hardware: {devices.Count} microphones");
    foreach (var device in devices)
        Console.WriteLine("Device: " + device.Name);
    if (devices.Count == 0)
    {
        Console.WriteLine("SKIP: no capture endpoint");
        return;
    }
    mic.Faulted += message => Console.WriteLine("AUDIO ERROR: " + message);
    var requested = args.SkipWhile(a => a != "--device").Skip(1).FirstOrDefault();
    var chosen = requested == null ? devices[0] : devices.First(d => d.Name.Contains(requested, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine("Testing: " + chosen.Name);
    mic.Start(chosen.Id);
    await Task.Delay(1500);
    Assert(mic.IsRunning && mic.Level.Frames > 0, "Capture produced no frames");
    Console.WriteLine($"PASS live WASAPI capture: {mic.Level.Frames} frames, peak {mic.Level.PeakDb:0.0} dB");
    mic.Muted = true;
    await Task.Delay(200);
    Assert(mic.Level.PeakDb <= -89, "Mute failed");
    Console.WriteLine("PASS live mute");
    mic.Muted = false;
    mic.GainDb = 3;
    await Task.Delay(200);
    using (var cancel = new CancellationTokenSource(100))
    {
        try
        {
            await mic.CalibrateAsync(cancel.Token);
            throw new Exception("Cancellation ignored");
        }
        catch (OperationCanceledException) { Console.WriteLine("PASS calibration cancellation"); }
    }
    var result = await mic.CalibrateAsync(CancellationToken.None);
    Console.WriteLine($"PASS live calibration completed: {result.Reason}, gain {result.GainDb:0.0} dB");
    mic.Stop();
    Assert(!mic.IsRunning);
    Console.WriteLine("PASS stop and dispose");
}









ProductTests.Run();
