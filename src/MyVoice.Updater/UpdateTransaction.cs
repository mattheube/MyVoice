using System.IO.Compression;
using System.Text.Json;
namespace MyVoice.Updater;

public sealed record InstallProgress(string Stage,int Completed,int Total);
public sealed record InstallJournal(string Target,string Stage,string Backup,string Phase);
/// <summary>Prepares siblings on the same volume, swaps only program directories, and preserves the previous version.</summary>
public sealed class UpdateTransaction
{
    public string Target {get;}
    public string Stage {get;}
    public string Backup {get;}
    public string Journal {get;}
    private bool swapped;
    public UpdateTransaction(string target,string transactionDirectory)
    {
        Target=Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
        if(Path.GetPathRoot(Target)==Target||!File.Exists(Path.Combine(Target,"MyVoice.exe")))throw new InvalidDataException("Dossier MyVoice invalide");
        AssertPlainPath(Target);
        var parent=Directory.GetParent(Target)!.FullName;var id=Guid.NewGuid().ToString("N");
        Stage=Path.Combine(parent,".myvoice-stage-"+id);Backup=Path.Combine(parent,".myvoice-backup-"+id);
        Directory.CreateDirectory(transactionDirectory);Journal=Path.Combine(transactionDirectory,"install-journal.json");
        Record("created");
    }
    public static void AssertPlainPath(string path)
    {
        for(var current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
            if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Liens de fichiers non autorisés pour la mise à jour");
    }
    public static void CopyTree(string source,string destination,Action<InstallProgress>? report=null)
    {
        AssertPlainPath(source);AssertPlainPath(destination);Directory.CreateDirectory(destination);
        var files=new List<string>();
        void Walk(string directory)
        {
            foreach(var child in Directory.EnumerateFileSystemEntries(directory))
            {
                if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Lien trouvé dans les fichiers programme");
                if(Directory.Exists(child))Walk(child);else files.Add(child);
            }
        }
        Walk(source);
        for(int i=0;i<files.Count;i++){var to=Path.Combine(destination,Path.GetRelativePath(source,files[i]));Directory.CreateDirectory(Path.GetDirectoryName(to)!);File.Copy(files[i],to,true);report?.Invoke(new("Préparation des fichiers",i+1,files.Count));}
    }
    public async Task PrepareAsync(string package,string sha256,Action<InstallProgress>? progress,CancellationToken token)
    {
        await using var stream=new FileStream(package,FileMode.Open,FileAccess.Read,FileShare.Read);
        await UpdateService.VerifyPackageAsync(stream,sha256,token);
        using var zip=new ZipArchive(stream,ZipArchiveMode.Read,true);
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long size=0;
        foreach(var entry in zip.Entries)
        {
            var name=entry.FullName.Replace('\\','/');var parts=name.TrimEnd('/').Split('/');
            if(string.IsNullOrWhiteSpace(name)||name.StartsWith('/')||parts.Any(p=>p is "" or "." or ".."||p.IndexOfAny(Path.GetInvalidFileNameChars())>=0||p.EndsWith('.')||p.EndsWith(' '))||!names.Add(name.TrimEnd('/')))
                throw new InvalidDataException("Chemin d’archive invalide ou dupliqué");
            if(((entry.ExternalAttributes>>16)&0xF000)==0xA000||(entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException("Lien interdit dans le package");
            size=checked(size+entry.Length);if(size>2L*1024*1024*1024||zip.Entries.Count>20000)throw new InvalidDataException("Archive trop volumineuse");
        }
        if(!names.Contains("MyVoice.exe")||!names.Contains("MyVoice.dll")||!names.Contains("MyVoice.runtimeconfig.json"))throw new InvalidDataException("Package MyVoice incomplet");
        CopyTree(Target,Stage,progress);int count=0;
        foreach(var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();var destination=Path.GetFullPath(Path.Combine(Stage,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
            if(!destination.StartsWith(Stage+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Sortie du dossier de préparation");
            AssertPlainPath(destination);
            if(entry.FullName.EndsWith('/'))Directory.CreateDirectory(destination);
            else{Directory.CreateDirectory(Path.GetDirectoryName(destination)!);await using var input=entry.Open();await using var output=File.Create(destination);await input.CopyToAsync(output,token);}
            progress?.Invoke(new("Installation",++count,zip.Entries.Count));
        }
        Record("prepared");
    }
    public void Commit()
    {
        AssertPlainPath(Target);AssertPlainPath(Stage);AssertSibling(Backup);AssertSibling(Stage);
        if(!Directory.Exists(Stage)||Directory.Exists(Backup))throw new InvalidOperationException("Transaction non préparée");
        Record("swapping");Directory.Move(Target,Backup);
        try{Directory.Move(Stage,Target);swapped=true;Record("awaiting-startup");}
        catch{if(!Directory.Exists(Target)&&Directory.Exists(Backup))Directory.Move(Backup,Target);Record("rolled-back");throw;}
    }
    public void Rollback()
    {
        if(!swapped)return;
        AssertPlainPath(Target);AssertPlainPath(Backup);AssertSibling(Backup);AssertSibling(Stage);
        if(Directory.Exists(Stage))throw new IOException("Dossier de récupération déjà présent");
        Directory.Move(Target,Stage);
        try{Directory.Move(Backup,Target);swapped=false;try{Record("rolled-back");}catch(IOException){ /* Restored files take priority over a full diagnostic disk. */ }}
        catch{if(!Directory.Exists(Target)&&Directory.Exists(Stage))Directory.Move(Stage,Target);throw;}
    }
    public void Complete()=>Record("completed");
    private void AssertSibling(string path){if(Directory.GetParent(path)?.FullName!=Directory.GetParent(Target)?.FullName||path==Target)throw new InvalidDataException("Dossier de transaction invalide");AssertPlainPath(path);}
    private void Record(string phase)
    {
        var temp=Journal+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new InstallJournal(Target,Stage,Backup,phase)));File.Move(temp,Journal,true);
    }
}
