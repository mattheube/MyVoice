using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MyVoice.Core;
namespace MyVoice.Infrastructure;

public sealed record PackageSound(string Name,string File,string? Cover,double Volume,bool Loop,string Category,double BassDb=0,double Saturation=0);
public sealed class CreationManifest
{
    public int SchemaVersion {get;set;}=1;
    public string Kind {get;set;}="soundboard";
    public string Title {get;set;}="";
    public string Version {get;set;}="1.0.0";
    public string? Cover {get;set;}
    public List<PackageSound> Sounds {get;set;}=[];
    public DesignedVoice? Preset {get;set;}
    public Dictionary<string,string> Files {get;set;}=[];
}

public static class CreationPackage
{
    public const long MaxArchive=128L*1024*1024,MaxExpanded=256L*1024*1024;
    private static readonly HashSet<string> Allowed=new(StringComparer.OrdinalIgnoreCase){".json",".wav",".mp3",".flac",".ogg",".m4a",".png",".jpg",".jpeg",".webp"};
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
            if(size>MaxExpanded||entry.Length>64L*1024*1024)throw new InvalidDataException("Taille décompressée excessive.");
        }
        var descriptor=archive.GetEntry("manifest.json")??throw new InvalidDataException("Manifeste absent.");
        if(descriptor.Length>1024*1024)throw new InvalidDataException("Manifeste excessif.");
        using var stream=descriptor.Open();var m=JsonSerializer.Deserialize<CreationManifest>(stream,Json)??throw new InvalidDataException("Manifeste invalide.");
        if(m.SchemaVersion!=1||m.Kind is not("soundboard" or "voice-preset")||string.IsNullOrWhiteSpace(m.Title)||m.Title.Length>100||!System.Version.TryParse(m.Version,out _)||m.Sounds.Count>200)throw new InvalidDataException("Package incompatible.");
        if(m.Kind=="voice-preset" && (m.Preset==null||!Finite(m.Preset)))throw new InvalidDataException("Preset invalide.");
        foreach(var entry in archive.Entries.Where(e=>e.FullName!="manifest.json"))
        {
            if(!m.Files.TryGetValue(entry.FullName,out var expected))throw new InvalidDataException("Fichier non déclaré.");
            using var bytes=entry.Open();if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Fichier altéré.");
        }
        foreach(var name in m.Files.Keys)if(!SafePath(name)||archive.GetEntry(name)==null)throw new InvalidDataException("Fichier manquant.");
        foreach(var sound in m.Sounds){Reference(sound.File,m);if(sound.Cover!=null)Reference(sound.Cover,m);if(!double.IsFinite(sound.Volume)||sound.Volume<0||!double.IsFinite(sound.BassDb)||!double.IsFinite(sound.Saturation))throw new InvalidDataException("Réglage invalide.");}
        if(m.Cover!=null)Reference(m.Cover,m);return m;
    }
    private static bool Finite(DesignedVoice v)=>new[]{v.Pitch,v.Bass,v.Mid,v.Treble,v.Delay,v.Distortion,v.Mix}.All(double.IsFinite)&&v.Pitch is >=.5 and <=2&&v.Mix is >=0 and <=1;
    private static void Reference(string path,CreationManifest m){if(!SafePath(path)||!m.Files.ContainsKey(path))throw new InvalidDataException("Référence invalide.");}
    public static (CreationManifest Manifest,string Directory) Extract(string file,string parent)
    {
        var manifest=Inspect(file);var destination=Path.Combine(Path.GetFullPath(parent),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);
        using var archive=ZipFile.OpenRead(file);
        foreach(var entry in archive.Entries)
        {
            var path=Path.GetFullPath(Path.Combine(destination,entry.FullName));
            if(!path.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Chemin interdit.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var input=entry.Open();using var output=new FileStream(path,FileMode.CreateNew);var buffer=new byte[65536];long written=0;int read;while((read=input.Read(buffer))>0){written+=read;if(written>entry.Length||written>64L*1024*1024)throw new InvalidDataException("Taille extraite incohérente.");output.Write(buffer,0,read);}if(written!=entry.Length)throw new InvalidDataException("Fichier incomplet.");
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
            m.Sounds.Add(new(sound.Name,audio,cover,sound.Volume,sound.Loop,sound.Folder,sound.BassDb,sound.Saturation));
        }
        Write(file,m,files);
    }
    public static void ExportPreset(string file,DesignedVoice preset)=>Write(file,new CreationManifest{Title=preset.Name,Kind="voice-preset",Preset=preset},[]);
    private static void Write(string file,CreationManifest m,Dictionary<string,string> files)
    {
        using(var zip=ZipFile.Open(file,ZipArchiveMode.Create)){foreach(var f in files)zip.CreateEntryFromFile(f.Value,f.Key);using var output=zip.CreateEntry("manifest.json").Open();JsonSerializer.Serialize(output,m,Json);}
        Inspect(file);
    }
}
