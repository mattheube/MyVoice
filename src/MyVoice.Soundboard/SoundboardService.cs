using System.Text.Json;
using MyVoice.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace MyVoice.Soundboard;
public sealed class SoundboardService : ISoundboardService, IDisposable
{
    private readonly string root;
    private bool writable=true;
    private readonly object gate = new();
    private readonly Dictionary<Guid, Playback> voices = new();
    public SoundLibrary Library
    {
        get;
    }
    public bool AllowMultiple
    {
        get; set;
    }
    public float MasterVolume { get; set; } = 1;
    public event Action<string>? Error;
    public SoundboardService(string root)
    {
        this.root = root;
        var path = Path.Combine(root, "soundboards", "library.json");
        try
        {
            Library = File.Exists(path) ? JsonSerializer.Deserialize<SoundLibrary>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (JsonException) { File.Copy(path, path + ".corrupt-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"), false); Library = new(); writable=false; }
        if (!File.Exists(path))
        {
            var presets = Path.Combine(AppContext.BaseDirectory, "sounds");
            if (Directory.Exists(presets))
            foreach (var file in Directory.GetFiles(presets, "*.wav"))
            {
                var dest = Path.Combine(root, "sounds", Guid.NewGuid() + ".wav");
                File.Copy(file, dest);
                Library.Sounds.Add(new SoundItem { Name = Path.GetFileNameWithoutExtension(file), File = dest });
            }
            Save();
        }
    }
    public void Save()
    {
        if(!writable) throw new InvalidDataException("Sound catalog unreadable; original preserved.");
        var path = Path.Combine(root, "soundboards", "library.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Library, new JsonSerializerOptions { WriteIndented = true }));
        if(File.Exists(path))
        {
            var history=Path.Combine(root,"backups","soundboard-history");Directory.CreateDirectory(history);
            var previous=File.ReadAllBytes(path);var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(previous));
            var snapshot=Path.Combine(history,hash+".json");if(!File.Exists(snapshot))File.WriteAllBytes(snapshot,previous);
            File.Copy(path,path+".backup",true);
        }
        File.Move(path + ".tmp", path, true);
    }
    public async Task<Guid> ImportAsync(string file, CancellationToken token)
    {
        return await Task.Run(() => { token.ThrowIfCancellationRequested(); using var probe = new AudioFileReader(file); if (probe.TotalTime.TotalSeconds <= 0) throw new InvalidDataException("Empty audio"); var id = Guid.NewGuid(); var dest = Path.Combine(root, "sounds", id + Path.GetExtension(file).ToLowerInvariant()); File.Copy(file, dest); var item = new SoundItem { Id = id, File = dest, Name = Path.GetFileNameWithoutExtension(file) }; lock (gate) Library.Sounds.Add(item); return id; }, token);
    }
    public void Play(Guid id)
    {
        lock (gate)
        {
            var item = Library.Sounds.First(s => s.Id == id);
            if (!AllowMultiple)
                StopAllLocked(id);
            if (voices.TryGetValue(id, out var existing))
            {
                if (item.PlaybackMode == "Toggle" && !existing.Paused)
                {
                    existing.Paused = true;
                    return;
                }
                existing.Paused = false;
                return;
            }
            if (voices.Count >= 16)
                throw new InvalidOperationException("Maximum: 16 sounds");
            voices[id] = new(item);
        }
    }
    public void Pause(Guid id)
    {
        lock (gate)
        if (voices.TryGetValue(id, out var p))
            p.Paused = !p.Paused;
    }
    public void Stop(Guid id)
    {
        lock (gate)
        {
            if (voices.Remove(id, out var p))
                p.Dispose();
        }
    }
    public void Restart(Guid id)
    {
        Stop(id);
        Play(id);
    }
    public void Seek(Guid id, double seconds)
    {
        lock (gate)
        if (voices.TryGetValue(id, out var p))
            p.Seek(seconds);
    }
    public (double Position, double Duration, bool Playing, bool Paused) State(Guid id)
    {
        lock (gate)
        {
            if (voices.TryGetValue(id, out var p))
                return (p.Reader.CurrentTime.TotalSeconds, p.Reader.TotalTime.TotalSeconds, !p.Paused, p.Paused);
        }
        return (0, 0, false, false);
    }
    public int ActiveCount
    {
        get
        {
            lock (gate)
                return voices.Values.Count(v => !v.Paused);
        }
    }
    public void StopAll()
    {
        lock (gate)
            StopAllLocked(null);
    }
    private void StopAllLocked(Guid? except)
    {
        foreach (var id in voices.Keys.Where(x => x != except).ToArray())
        {
            voices[id].Dispose();
            voices.Remove(id);
        }
    }
    public void Mix(Span<float> monitor, Span<float> virtualOut)
    {
        lock (gate)
        {
            foreach (var p in voices.Values)
            {
                if (p.Paused || p.Finished)
                    continue;
                try
                {
                    int total = 0;
                    while (total < monitor.Length)
                    {
                        int n = p.Provider.Read(p.Buffer, total, monitor.Length - total);
                        if (n == 0)
                        {
                            if (!p.Item.Loop && p.Item.PlaybackMode != "Hold")
                            {
                                p.Finished = true;
                                break;
                            }
                            p.Seek(0);
                            n = p.Provider.Read(p.Buffer, total, monitor.Length - total);
                            if (n == 0)
                            {
                                p.Finished = true;
                                break;
                            }
                        }
                        total += n;
                    }
                    p.Tone.Process(p.Buffer,total,p.Item.Volume * MasterVolume,p.Item.BassDb,p.Item.Saturation);
                    for (int i = 0; i < total; i++)
                    {
                        float x = p.Buffer[i];
                        if (p.Item.HearMyself)
                            monitor[i] += x;
                        if (p.Item.SendToMic)
                            virtualOut[i] += x;
                    }
                }
                catch (Exception e) { p.Finished = true; Error?.Invoke(e.Message); }
            }
            foreach (var id in voices.Where(p => p.Value.Finished).Select(p => p.Key).ToArray())
            {
                voices[id].Dispose();
                voices.Remove(id);
            }
        }
    }
    public void Dispose()
    {
        StopAll();
        Save();
    }
    private sealed class Playback : IDisposable
    {
        public SoundItem Item
        {
            get;
        }
        public AudioFileReader Reader
        {
            get;
        }
        public ISampleProvider Provider
        {
            get; private set;
        }
        public SoundToneProcessor Tone {get;} = new();
        public float[] Buffer { get; } = new float[960];
        public bool Paused, Finished;
        public Playback(SoundItem item)
        {
            Item = item;
            Reader = new(item.File);
            try { Provider = CreateProvider(); } catch { Reader.Dispose(); throw; }
        }
        private ISampleProvider CreateProvider()
        {
            ISampleProvider p = Reader;
            if (p.WaveFormat.SampleRate != 48000)
                p = new WdlResamplingSampleProvider(p, 48000);
            if (p.WaveFormat.Channels == 1)
                p = new MonoToStereoSampleProvider(p);
            if (p.WaveFormat.Channels != 2)
                throw new NotSupportedException("Use mono or stereo audio");
            return p;
        }
        public void Seek(double seconds)
        {
            Reader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, Math.Max(0, Reader.TotalTime.TotalSeconds - .001)));
            Provider = CreateProvider();
            Finished = false;
        }
        public void Dispose() => Reader.Dispose();
    }
}
