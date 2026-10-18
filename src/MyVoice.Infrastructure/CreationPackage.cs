using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MyVoice.Core;
namespace MyVoice.Infrastructure;

public sealed record PackageSound(string Name,string File,string? Cover,double Volume,bool Loop,string Category,double BassDb=0,double Saturation=0,string? SourceContentId=null,string Description="",string[]? Tags=null);
public sealed class CreationManifest
{
    public int SchemaVersion {get;set;}=1;
    public string Kind {get;set;}="soundboard";
    public string Title {get;set;}="";
    public string Version {get;set;}="1.0.0";
    public string? Cover {get;set;}
    public List<PackageSound> Sounds {get;set;}=[];
    public DesignedVoice? Preset {get;set;}
    public VoiceReference? AiVoice {get;set;}
    public Dictionary<string,string> Files {get;set;}=[];
}

public static class CreationPackage
{
    public const long MaxArchive=128L*1024*1024,MaxExpanded=256L*1024*1024;
    private static readonly HashSet<string> Allowed=new(StringComparer.OrdinalIgnoreCase){".json",".wav",".mp3",".flac",".ogg",".m4a",".png",".jpg",".jpeg",".webp",".gif"};
    private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
    public static bool SafePath(string name)=>!string.IsNullOrWhiteSpace(name)&&!name.StartsWith('/')&&!name.Contains('\\')&&!name.Contains(':')&&!name.Contains('\0')&&!name.Split('/').Any(p=>p is "" or "." or ".." || p.EndsWith('.') || p.EndsWith(' ') || System.Text.RegularExpressions.Regex.IsMatch(p,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    public static CreationManifest Inspect(string file)
    {
        if(new FileInfo(file).Length>MaxArchive)throw new InvalidDataException("Package trop volumineux (128 Mo maximum).");
        using var archive=ZipFile.OpenRead(file);
        if(archive.Entries.Count>512)throw new InvalidDataException("Trop de fichiers.");
        long size=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in archive.Entries)
        {
            if(!SafePath(entry.FullName)||!Allowed.Contains(Path.GetExtension(entry.FullName))||!names.Add(entry.FullName)||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Chemin ou type de fichier interdit.");
            size=checked(size+entry.Length);
            if(size>MaxExpanded||entry.Length>64L*1024*1024)throw new InvalidDataException("Taille dÃ©compressÃ©e excessive.");
        }
        var descriptor=archive.GetEntry("manifest.json")??throw new InvalidDataException("Manifeste absent.");
        if(descriptor.Length>1024*1024)throw new InvalidDataException("Manifeste excessif.");
        using var stream=descriptor.Open();var m=JsonSerializer.Deserialize<CreationManifest>(stream,Json)??throw new InvalidDataException("Manifeste invalide.");
        if(m.SchemaVersion!=1||m.Kind is not("soundboard" or "voice-preset" or "ai-voice")||string.IsNullOrWhiteSpace(m.Title)||m.Title.Length>100||!System.Version.TryParse(m.Version,out _)||m.Sounds.Count>200)throw new InvalidDataException("Package incompatible.");
        if(m.Kind=="voice-preset" && (m.Preset==null||!Finite(m.Preset)))throw new InvalidDataException("Preset invalide.");
        foreach(var entry in archive.Entries.Where(e=>e.FullName!="manifest.json"))
        {
            if(!m.Files.TryGetValue(entry.FullName,out var expected))throw new InvalidDataException("Fichier non dÃ©clarÃ©.");
            using var bytes=entry.Open();if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Fichier altÃ©rÃ©.");
        }
        foreach(var name in m.Files.Keys)if(!SafePath(name)||archive.GetEntry(name)==null)throw new InvalidDataException("Fichier manquant.");
        foreach(var sound in m.Sounds){Reference(sound.File,m);if(sound.Cover!=null)Reference(sound.Cover,m);if(!double.IsFinite(sound.Volume)||sound.Volume<0||!double.IsFinite(sound.BassDb)||!double.IsFinite(sound.Saturation))throw new InvalidDataException("RÃ©glage invalide.");}
        if(m.Cover!=null)Reference(m.Cover,m);
        if(m.Kind=="ai-voice"){
            var v=m.AiVoice??throw new InvalidDataException("Référence IA absente.");Reference(v.ReferenceFile,m);
            if(v.Samples.Count>100||v.Revisions.Count>0||v.AdditionalData?.Count>0||!double.IsFinite(v.Duration)||v.Duration<=0)throw new InvalidDataException("Référence IA invalide.");
            foreach(var sample in v.Samples){Reference(sample.OriginalFile,m);foreach(var segment in sample.Segments)Reference(segment.File,m);}
            if(v.Cover!=null)Reference(v.Cover,m);
        }
        return m;
    }
    private static bool Finite(DesignedVoice v)=>new[]{v.Pitch,v.Bass,v.Mid,v.Treble,v.Delay,v.Distortion,v.Mix}.All(double.IsFinite)&&v.Pitch is >=.5 and <=2&&v.Mix is >=0 and <=1;
    private static void Reference(string path,CreationManifest m){if(!SafePath(path)||!m.Files.ContainsKey(path))throw new InvalidDataException("RÃ©fÃ©rence invalide.");}
    public static (CreationManifest Manifest,string Directory) Extract(string file,string parent)
    {
        var manifest=Inspect(file);var destination=Path.Combine(Path.GetFullPath(parent),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);
        using var archive=ZipFile.OpenRead(file);
        foreach(var entry in archive.Entries)
        {
            var path=Path.GetFullPath(Path.Combine(destination,entry.FullName));
            if(!path.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Chemin interdit.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var input=entry.Open();using var output=new FileStream(path,FileMode.CreateNew);var buffer=new byte[65536];long written=0;int read;while((read=input.Read(buffer))>0){written+=read;if(written>entry.Length||written>64L*1024*1024)throw new InvalidDataException("Taille extraite incohÃ©rente.");output.Write(buffer,0,read);}if(written!=entry.Length)throw new InvalidDataException("Fichier incomplet.");
        }
        return(manifest,destination);
    }
    public static void ExportBoard(string file,SoundFolder folder,IEnumerable<SoundItem> sounds)
    {
        var m=new CreationManifest{Title=folder.Name};var files=new Dictionary<string,string>();int i=0;
        string Add(string source,string prefix){var name=prefix+Path.GetExtension(source).ToLowerInvariant();if(!SafePath(name)||!Allowed.Contains(Path.GetExtension(name)))throw new InvalidDataException("Fichier non pris en charge.");files.Add(name,source);m.Files[name]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));return name;}
        if(folder.Cover!=null && File.Exists(folder.Cover))m.Cover=Add(folder.Cover,"cover");
        foreach(var sound in sounds)
        {
            var audio=Add(sound.File,"sounds/"+(++i));var cover=sound.Cover!=null&&File.Exists(sound.Cover)?Add(sound.Cover,"covers/"+i):null;
            m.Sounds.Add(new(sound.Name,audio,cover,sound.Volume,sound.Loop,sound.Category,sound.BassDb,sound.Saturation,sound.SourceContentId,sound.Description,sound.Tags.ToArray()));
        }
        Write(file,m,files);
    }
    public static void ExportPreset(string file,DesignedVoice preset)=>Write(file,new CreationManifest{Title=preset.Name,Kind="voice-preset",Preset=preset},[]);
    public static void ExportAiVoice(string file,VoiceReference voice)
    {
        if(!string.IsNullOrEmpty(voice.SourceContentId))throw new InvalidDataException("Une voix importée ne peut pas être republiée.");
        var m=new CreationManifest{Title=voice.Name,Kind="ai-voice"};var files=new Dictionary<string,string>();
        string Add(string path,string name){name+=Path.GetExtension(path).ToLowerInvariant();files[name]=path;using var stream=File.OpenRead(path);m.Files[name]=Convert.ToHexString(SHA256.HashData(stream));return name;}
        var v=new VoiceReference{Name=voice.Name,ReferenceFile=Add(voice.ReferenceFile,"reference"),Duration=voice.Duration,Backend=voice.Backend,BackendVersion=voice.BackendVersion};
        if(voice.Cover is {} cover&&File.Exists(cover))m.Cover=v.Cover=Add(cover,"cover");
        // Export only the selected usable segments, never local paths, revisions or excluded originals.
        int i=0;foreach(var sample in voice.Samples.Where(s=>!s.Excluded))foreach(var segment in sample.Segments.OrderByDescending(s=>s.Score).Take(3).Where(s=>File.Exists(s.File))){var path=Add(segment.File,"samples/"+(++i));v.Samples.Add(new DatasetSample{Name="Extrait "+i,OriginalFile=path,Duration=segment.Seconds,UsableSeconds=segment.Seconds,Segments=[new(path,segment.Seconds,segment.Score)]});}
        m.AiVoice=v;Write(file,m,files);
    }
    private static void Write(string file,CreationManifest m,Dictionary<string,string> files)
    {
        using(var zip=ZipFile.Open(file,ZipArchiveMode.Create)){foreach(var f in files)zip.CreateEntryFromFile(f.Value,f.Key);using var output=zip.CreateEntry("manifest.json").Open();JsonSerializer.Serialize(output,m,Json);}
        Inspect(file);
    }
}
