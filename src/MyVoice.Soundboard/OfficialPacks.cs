using System.Security.Cryptography;
using System.Text.Json;
using MyVoice.Core;
namespace MyVoice.Soundboard;

public sealed record OfficialSound(string Name,string File,string Sha256);
public sealed record OfficialPack(string Id,string Title,string Description,List<OfficialSound> Sounds)
{
    public string? Cover {get;set;}
    public int Count=>Sounds.Count;
}
public static class OfficialPacks
{
    public static List<OfficialPack> Load(string directory)=>JsonSerializer.Deserialize<List<OfficialPack>>(File.ReadAllText(Path.Combine(directory,"catalog.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
    public static SoundFolder Install(OfficialPack pack,string bundledDirectory,string userRoot,SoundLibrary library)
    {
        var existing=library.Folders.FirstOrDefault(f=>f.OfficialPackId==pack.Id);if(existing!=null)return existing;
        // Verify all bundled files before changing the user's catalog.
        var items=new List<SoundItem>();
        foreach(var sound in pack.Sounds){
            var path=Path.GetFullPath(Path.Combine(bundledDirectory,sound.File));
            if(!path.StartsWith(Path.GetFullPath(bundledDirectory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid official asset path");
            using var stream=File.OpenRead(path);if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(sound.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Official sound checksum mismatch");
            items.Add(new(){Name=sound.Name,File=path,Cover=pack.Cover,Description=pack.Description,Category="Sfx",Volume=.8});
        }
        string name=pack.Title;int suffix=2;while(library.Folders.Any(f=>f.Name==name))name=pack.Title+" ("+suffix+++ ")";
        var folder=new SoundFolder{Name=name,OfficialPackId=pack.Id,Cover=pack.Cover};
        string destination=Path.Combine(userRoot,"sounds","official",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);
        for(int i=0;i<items.Count;i++){string file=Path.Combine(destination,i+".wav");File.Copy(items[i].File,file);items[i].File=file;items[i].Folder=name;}
        library.Folders.Add(folder);library.Sounds.AddRange(items);return folder;
    }
}
