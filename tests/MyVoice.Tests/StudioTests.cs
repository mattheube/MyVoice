using MyVoice.Core;
using MyVoice.Audio;
using MyVoice.Soundboard;
using MyVoice.Infrastructure;
using MyVoice.Updater;
using NAudio.Wave;
using System.Security.Cryptography;
using System.Text.Json;
public static class StudioTests
{
    public static async Task Run()
    {
        int passed = 0;
        void Assert(bool ok, string name)
        {
            if (!ok)
                throw new Exception(name);
            passed++;
            Console.WriteLine("PASS " + name);
        }
        var ring = new SampleRing(4);
        ring.Write(new float[] { 1, 2, 3, 4, 5, 6 });
        var taken = new float[4];
        ring.Take(taken);
        Assert(taken.SequenceEqual(new float[] { 3, 4, 5, 6 }) && ring.Dropped == 2, "Bounded ring drops oldest audio");
        ring.Clear();
        Assert(ring.Take(taken) == 0 && taken.All(x => x == 0), "Underrun produces silence");
        float[] mono = [.2f, .3f], local = new float[4], remote = new float[4];
        AudioRouting.MixMicrophone(mono, local, remote, false, false, true);
        Assert(local.All(x => x == 0) && remote[0] == .2f, "Microphone routing remote only");
        Array.Clear(local);
        Array.Clear(remote);
        AudioRouting.MixMicrophone(mono, local, remote, true, true, true);
        Assert(local.All(x => x == 0) && remote.All(x => x == 0), "Microphone mute affects both destinations");
        var root = Path.Combine(Path.GetTempPath(), "MyVoice-Studio-" + Guid.NewGuid().ToString("N"));
        new LocalStore(root);
        var source = Path.Combine(root, "test.wav");
        using (var w = new WaveFileWriter(source, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1)))
        {
            var data = Enumerable.Range(0, 48000).Select(i => (float)(.1 * Math.Sin(2 * Math.PI * 440 * i / 48000))).ToArray();
            w.WriteSamples(data, 0, data.Length);
        }
        using (var sounds = new SoundboardService(root))
        {
            var id = await sounds.ImportAsync(source, CancellationToken.None);
            var item = sounds.Library.Sounds.First(s => s.Id == id);
            Assert(item.File != source && File.Exists(item.File), "Imported sound is copied into library");
            var l = new float[960];
            var r = new float[960];
            foreach (var hear in new[] { false, true })
            foreach (var send in new[] { false, true })
            {
                item.HearMyself = hear;
                item.SendToMic = send;
                sounds.Restart(id);
                Array.Clear(l);
                Array.Clear(r);
                sounds.Mix(l, r);
                Assert((l.Any(x => x != 0) == hear) && (r.Any(x => x != 0) == send), $"Sound routing local={hear} remote={send}");
            }
            sounds.Pause(id);
            var before = sounds.State(id).Position;
            sounds.Mix(l, r);
            Assert(sounds.State(id).Position == before, "Pause preserves timeline");
            sounds.Pause(id);
            sounds.Seek(id, .6);
            Assert(Math.Abs(sounds.State(id).Position - .6) < .02, "Seek moves sound timeline");
            sounds.Restart(id);
            Assert(sounds.State(id).Position < .02, "Restart rewinds sound");
            item.Loop = true;
            sounds.Seek(id, .998);
            sounds.Mix(l, r);
            Assert(sounds.State(id).Playing && sounds.State(id).Position < .1, "Loop wraps end of file");
            sounds.Pause(id);
            sounds.StopAll();
            Assert(!sounds.State(id).Paused && sounds.ActiveCount == 0, "Stop All includes paused loops");
            sounds.AllowMultiple = true;
            sounds.Play(id);
            var id2 = await sounds.ImportAsync(source, CancellationToken.None);
            sounds.Play(id2);
            Assert(sounds.ActiveCount == 2, "Multiple simultaneous sounds");
            sounds.AllowMultiple = false;
            sounds.Restart(id);
            Assert(sounds.ActiveCount == 1, "Single-sound mode replaces previous sound");
            sounds.Save();
        }
        foreach (var name in new[] { "Clean", "Deep", "High", "Robot", "Radio", "Walkie Talkie", "Megaphone", "Echo", "Demon", "Tiny" })
        {
            var processor = new VoiceProcessor { Preset = name, Settings = new ProcessingSettings { Gate = false, NoiseSuppression = 0, Compressor = false } };
            double difference = 0;
            for (int block = 0; block < 40; block++)
            {
                var data = Enumerable.Range(0, 480).Select(i => (float)(.2 * Math.Sin(2 * Math.PI * 440 * (block * 480 + i) / 48000))).ToArray();
                var dry = (float[])data.Clone();
                processor.Process(data);
                if (data.Any(x => !float.IsFinite(x)))
                    throw new Exception("Nonfinite effect " + name);
                difference += data.Zip(dry, (a, b) => Math.Abs(a - b)).Sum();
            }
            Assert(name == "Clean" ? difference < 1 : difference > 1, "DSP produces actual " + name + " processing");
        }
        var denoise = new SpectralDenoiser();
        double energy = 0;
        for (int i = 0; i < 48000; i++)
        {
            var v = denoise.Process((float)(.02 * Math.Sin(2 * Math.PI * 1000 * i / 48000)), 2);
            if (i > 24000)
                energy += v * v;
        }
        Assert(double.IsFinite(energy) && energy < 3, "Spectral denoiser attenuates stationary tone");
        var limiter = new PeakLimiter();
        float[] loud = [5, -8, .5f];
        limiter.Process(loud);
        Assert(loud.All(x => Math.Abs(x) <= .98f), "Master limiter contains mixed peaks");
        var fake = Path.Combine(root, "release.exe");
        await File.WriteAllBytesAsync(fake, [0x4D, 0x5A, 0, 0]);
        var manifest = Path.Combine(root, "latest.json");
        var release = new ReleaseManifest("0.9.0", "release.exe", Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fake))));
        await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(release));
        var updater = new UpdateService();
        var update = await updater.CheckAsync(manifest, new Version(0, 8, 0), CancellationToken.None);
        Assert(update != null, "Updater discovers newer local release");
        var dest = await updater.DownloadAsync(update!, manifest, Path.Combine(root, "cache"), CancellationToken.None);
        Assert(File.Exists(dest), "Updater verifies SHA-256 before staging");
        try
        {
            await updater.DownloadAsync(release with
            {
                Sha256 = new string('0', 64)
            }, manifest, Path.Combine(root, "cache"), CancellationToken.None);
            throw new Exception("Checksum accepted");
        }
        catch (InvalidDataException) { Assert(true, "Updater rejects checksum mismatch"); }
        Console.WriteLine($"{passed} studio tests passed");
    }
}
