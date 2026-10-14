using System.Text.Json;
using MyVoice.Core;
namespace MyVoice.Infrastructure;
public sealed class MixerStore(string root)
{
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    private string FileName=>Path.Combine(root,"config","mixer.json");
    public string? Recovery {get;private set;}
    private bool writable=true;
    public MixerState Load(AppSettings legacy)
    {
        if(!File.Exists(FileName)){var fresh=new MixerState{Current=MixerState.Recommended(legacy)};Save(fresh);return fresh;}
        foreach(var file in new[]{FileName,FileName+".backup"})try{if(!File.Exists(file))continue;var value=JsonSerializer.Deserialize<MixerState>(File.ReadAllText(file))??throw new InvalidDataException();if(value.Profiles==null||value.MidiMappings==null)throw new InvalidDataException();Validate(value.Current);foreach(var p in value.Profiles)Validate(p);if(value.SchemaVersion!=1||value.Profiles.Count>50||value.MidiMappings.Count>200)throw new InvalidDataException();if(file!=FileName){Recovery="Mixer restauré depuis sa sauvegarde.";File.Copy(FileName,FileName+".corrupt-"+DateTime.UtcNow.Ticks);File.Copy(file,FileName,true);}return value;}catch(Exception e)when(e is JsonException or IOException or InvalidDataException or ArgumentException){Recovery="Configuration Mixer illisible ; original conservé. Le routage historique reste disponible.";}
        writable=false;return new(){Current=MixerState.Recommended(legacy)};
    }
    public void Save(MixerState state)
    {
        if(!writable)throw new InvalidDataException(Recovery);
        if(state.Profiles.Count>50||state.MidiMappings.Count>200)throw new InvalidDataException("Limite de 50 profils ou 200 commandes MIDI atteinte.");
        foreach(var profile in state.Profiles)Validate(profile);
        Validate(state.Current);Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);File.WriteAllText(FileName+".tmp",JsonSerializer.Serialize(state,Json));if(File.Exists(FileName))File.Copy(FileName,FileName+".backup",true);File.Move(FileName+".tmp",FileName,true);
    }
    public static void Validate(AudioProfile p)
    {
        if(p==null||p.Channels==null||p.Outputs==null||p.Routes==null||p.Channels.Any(c=>c==null)||p.Outputs.Any(b=>b==null)||p.Routes.Any(r=>r==null))throw new InvalidDataException("Profil audio incomplet.");
        if(p.Channels.Any(c=>c.Type=="microphone"&&c.Id!="mic"||c.Type=="soundboard"&&c.Id!="soundboard"))throw new InvalidDataException("Le microphone principal et le bus Soundboard ne peuvent pas être dupliqués.");
        if(string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>80||p.Channels.Count is <2 or >24||p.Outputs.Count is <2 or >12||p.Routes.Count>288)throw new InvalidDataException("Profil audio trop volumineux ou incomplet.");
        if(!p.Channels.Any(c=>c.Id=="mic"&&c.Type=="microphone")||!p.Channels.Any(c=>c.Id=="soundboard"&&c.Type=="soundboard")||!p.Outputs.Any(b=>b.Id=="monitor")||!p.Outputs.Any(b=>b.Id=="virtual"))throw new InvalidDataException("Canaux essentiels absents.");
        if(p.Channels.Select(c=>c.Id).Distinct().Count()!=p.Channels.Count||p.Outputs.Select(b=>b.Id).Distinct().Count()!=p.Outputs.Count||p.Routes.Select(r=>(r.ChannelId,r.BusId)).Distinct().Count()!=p.Routes.Count)throw new InvalidDataException("Identifiants dupliqués.");
        foreach(var c in p.Channels){if(string.IsNullOrWhiteSpace(c.Id)||c.Id.Length>80||string.IsNullOrWhiteSpace(c.Name)||c.Name.Length>80||c.Type is not("microphone" or "soundboard" or "capture" or "system" or "application")||c.DeviceMode is not("Fixed" or "Default")||c.Effects==null)throw new InvalidDataException("Source invalide.");var e=c.Effects;if(!new[]{c.Volume,c.GainDb,c.Pan,e.HighPassHz,e.BassDb,e.MidDb,e.TrebleDb,e.GateThreshold,e.CompressorThreshold,e.CompressorRatio,e.Delay,e.Reverb,e.GateAttackMs,e.GateHoldMs,e.GateReleaseMs,e.CompressorAttackMs,e.CompressorReleaseMs,e.MakeupDb}.All(double.IsFinite)||c.Volume is <0 or >1e6||c.GainDb is <-60 or >30||Math.Abs(c.Pan)>1||e.HighPassHz is <0 or >1000||Math.Abs(e.BassDb)>18||Math.Abs(e.MidDb)>18||Math.Abs(e.TrebleDb)>18||e.NoiseReduction is <0 or >3||e.GateThreshold is <-90 or >0||e.CompressorThreshold is <-60 or >0||e.CompressorRatio is <1 or >20||e.Delay is <0 or >1||e.Reverb is <0 or >1||e.GateAttackMs is <1 or >200||e.GateHoldMs is <0 or >2000||e.GateReleaseMs is <1 or >2000||e.CompressorAttackMs is <1 or >200||e.CompressorReleaseMs is <1 or >2000||e.MakeupDb is <-12 or >18)throw new InvalidDataException("Effet ou niveau invalide.");}
        foreach(var b in p.Outputs)if(string.IsNullOrWhiteSpace(b.Id)||b.Id.Length>80||string.IsNullOrWhiteSpace(b.Name)||b.Name.Length>80||!double.IsFinite(b.Volume)||b.Volume is <0 or >1e6||b.DeviceMode is not("Internal" or "Default" or "Fixed")||b.Backend is not("WASAPI" or "MME" or "ASIO")||b.BufferMs is <10 or >200)throw new InvalidDataException("Sortie invalide.");
        foreach(var b in p.Outputs)if(b.Equalizer==null||b.Equalizer.Count>8||b.Equalizer.Any(e=>e==null||!double.IsFinite(e.Frequency)||!double.IsFinite(e.GainDb)||!double.IsFinite(e.Q)||e.Frequency is <20 or >20000||Math.Abs(e.GainDb)>18||e.Q is <.1 or >18))throw new InvalidDataException("Égaliseur de sortie invalide.");
        foreach(var r in p.Routes)if(!p.Channels.Any(c=>c.Id==r.ChannelId)||!p.Outputs.Any(b=>b.Id==r.BusId))throw new InvalidDataException("Routage orphelin.");
    }
    public static void Export(string file,AudioProfile profile)
    {
        Validate(profile);
        // Endpoint IDs and local application identity must never travel to another computer.
        var portable=profile with{Channels=profile.Channels.Select(c=>c with{DeviceId=null,DeviceName=null,ApplicationName=null,CaptureEnabled=false}).ToList(),Outputs=profile.Outputs.Select(b=>b with{DeviceId=null,DeviceName=null,DeviceMode="Internal"}).ToList()};
        File.WriteAllText(file,JsonSerializer.Serialize(portable,Json));
    }
    public static AudioProfile Import(string file)
    {
        if(new FileInfo(file).Length>256*1024)throw new InvalidDataException("Profil trop volumineux.");
        var p=JsonSerializer.Deserialize<AudioProfile>(File.ReadAllText(file))??throw new InvalidDataException("Profil invalide.");Validate(p);
        return p with{Id=Guid.NewGuid().ToString("N"),Channels=p.Channels.Select(c=>c with{DeviceId=null,DeviceName=null,ApplicationName=null,CaptureEnabled=false}).ToList(),Outputs=p.Outputs.Select(b=>b with{DeviceMode="Internal",DeviceId=null,DeviceName=null}).ToList()};
    }
}
