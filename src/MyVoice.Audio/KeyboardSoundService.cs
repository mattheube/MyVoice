using System.Text.Json;
using MyVoice.Core;
using NAudio.Wave;
namespace MyVoice.Audio;

/// <summary>Only the selected pack is resident. Fixed polyphony; no disk IO in Trigger or Render.</summary>
public sealed class KeyboardSoundService : IDisposable
{
    private struct Voice {public float[]? Samples;public double Position,Step;public float Gain;}
    private readonly object gate=new();
    private readonly Voice[] voices=new Voice[32];
    private Dictionary<string,float[][]>? samples;
    private KeyboardSettings settings=new();
    private int oldest;
    public long LoadedBytes {get;private set;}
    public int ActiveVoices {get{lock(gate)return voices.Count(v=>v.Samples!=null);}}
    public static KeyboardPack[] Catalog(string root)=>Directory.Exists(root)?Directory.GetDirectories(root).Select(p=>ReadManifest(Path.Combine(p,"manifest.json"))).Where(p=>p!=null).Cast<KeyboardPack>().OrderBy(p=>p.Name).ToArray():[];
    private static KeyboardPack? ReadManifest(string path){try{if(new FileInfo(path).Length>65536)return null;return JsonSerializer.Deserialize<KeyboardPack>(File.ReadAllText(path));}catch{return null;}}
    public void Load(string root,KeyboardSettings next)
    {
        if(!next.Enabled){Disable();return;}
        if(!double.IsFinite(next.Volume)||next.Volume is <0 or >2||!double.IsFinite(next.Pitch)||Math.Abs(next.Pitch)>6||!double.IsFinite(next.Intensity)||next.Intensity is <0 or >1||!double.IsFinite(next.SpacebarVolume)||next.SpacebarVolume is <0 or >2)throw new InvalidDataException("Réglages clavier invalides.");
        lock(gate){if(samples!=null&&settings.Pack==next.Pack){settings=next;return;}}
        if(string.IsNullOrWhiteSpace(next.Pack)||next.Pack!=Path.GetFileName(next.Pack))throw new InvalidDataException("Pack invalide.");
        string folder=Path.GetFullPath(Path.Combine(root,next.Pack));var manifest=ReadManifest(Path.Combine(folder,"manifest.json"))??throw new InvalidDataException("Pack clavier introuvable.");
        var loaded=new Dictionary<string,float[][]>();long bytes=0;
        foreach(var pair in manifest.Samples)
        {
            if(pair.Value.Length is <1 or >24)throw new InvalidDataException("Variations de pack invalides.");
            var variants=new List<float[]>();
            foreach(var relative in pair.Value)
            {
                var path=Path.GetFullPath(Path.Combine(folder,relative));
                if(!path.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".wav",StringComparison.OrdinalIgnoreCase)||new FileInfo(path).Length>100000)throw new InvalidDataException("Sample invalide.");
                using var reader=new AudioFileReader(path);
                if(reader.WaveFormat.SampleRate!=48000||reader.WaveFormat.Channels!=1||reader.TotalTime.TotalSeconds>.5)throw new InvalidDataException("Samples mono 48 kHz, durée maximale 500 ms.");
                var buffer=new float[(int)(reader.Length/4)];int read=reader.Read(buffer,0,buffer.Length);if(read!=buffer.Length||buffer.Any(x=>!float.IsFinite(x)||Math.Abs(x)>1))throw new InvalidDataException("PCM clavier invalide.");
                bytes+=buffer.Length*4;if(bytes>8*1024*1024)throw new InvalidDataException("Pack clavier trop lourd.");variants.Add(buffer);
            }
            loaded[pair.Key]=variants.ToArray();
        }
        foreach(var kind in Enum.GetValues<KeyboardKeyKind>())foreach(var edge in new[]{"down","up"})if(!loaded.ContainsKey(kind.ToString().ToLowerInvariant()+"/"+edge))throw new InvalidDataException("Pack clavier incomplet.");
        lock(gate){Array.Clear(voices);samples=loaded;settings=next;LoadedBytes=bytes;}
    }
    public void Trigger(KeyboardKeyKind kind,bool up)
    {
        lock(gate)
        {
            if(samples==null)return;
            // A fixed small category identifier; no key codes, text or history enter the audio service.
            var variants=samples[kind.ToString().ToLowerInvariant()+(up?"/up":"/down")];
            var clip=variants[Random.Shared.Next(variants.Length)];int slot=Array.FindIndex(voices,v=>v.Samples==null);
            if(slot<0){slot=oldest;oldest=(oldest+1)%voices.Length;}
            voices[slot]=new(){Samples=clip,Step=Math.Pow(2,(settings.Pitch+(Random.Shared.NextDouble()-.5)*.16)/12),Gain=(float)(settings.Volume*(.9+Random.Shared.NextDouble()*.2)*(.7+.6*settings.Intensity)*(kind==KeyboardKeyKind.Space?settings.SpacebarVolume:1))};
        }
    }
    public void Render(Span<float> stereo)
    {
        stereo.Clear();lock(gate)
        {
            for(int v=0;v<voices.Length;v++)
            {
                ref var voice=ref voices[v];if(voice.Samples is not {} clip)continue;
                for(int i=0;i<stereo.Length;i+=2)
                {
                    int at=(int)voice.Position;if(at>=clip.Length-1){voice.Samples=null;break;}
                    float value=(float)(clip[at]+(clip[at+1]-clip[at])*(voice.Position-at))*voice.Gain;
                    stereo[i]+=value;stereo[i+1]+=value;voice.Position+=voice.Step;
                }
            }
        }
        for(int i=0;i<stereo.Length;i++)stereo[i]=Math.Clamp(stereo[i],-.9f,.9f);
    }
    public void Disable(){lock(gate){samples=null;Array.Clear(voices);LoadedBytes=0;}}
    public void Dispose()=>Disable();
}
