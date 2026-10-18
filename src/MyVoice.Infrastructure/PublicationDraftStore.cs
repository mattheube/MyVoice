namespace MyVoice.Infrastructure;
public sealed record PublicationDraftMetadata(string Title,string Description,string Visibility,string Category,string Tags);
public sealed class PublicationDraftStore(string root)
{
    public string PackagePath=>Path.Combine(root,"drafts","publication.myvoice");
    public string? PreparedCoverPath {get;private set;}
    public async Task<CreationManifest> PrepareAsync(string source, PublicationDraftMetadata? metadata=null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PackagePath)!);
        var temporary=PackagePath+"."+Guid.NewGuid().ToString("N")+".part";
        try
        {
            await using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(input.Length>CreationPackage.MaxArchive)throw new InvalidDataException("Package trop volumineux (128 Mo maximum).");
                await using var output=File.Create(temporary);await input.CopyToAsync(output);
            }
            var manifest=await Task.Run(()=>CreationPackage.Inspect(temporary));
            if(manifest.Sounds.Any(s=>!string.IsNullOrEmpty(s.SourceContentId))||!string.IsNullOrEmpty(manifest.Preset?.SourceContentId)||!string.IsNullOrEmpty(manifest.AiVoice?.SourceContentId))
                throw new InvalidDataException("Ce package contient une création importée. Elle peut être conservée dans votre bibliothèque, mais pas republiée comme votre création.");
            string? cover=null;
            if(manifest.Cover is {} image)
            {
                var extension=Path.GetExtension(image).ToLowerInvariant();
                if(extension is not(".png" or ".jpg" or ".jpeg" or ".webp" or ".gif"))throw new InvalidDataException("La couverture doit être une image.");
                using var archive=System.IO.Compression.ZipFile.OpenRead(temporary);var entry=archive.GetEntry(image)!;
                if(entry.Length>20_000_000)throw new InvalidDataException("Couverture limitée à 20 Mo.");
                cover=Path.Combine(root,"drafts",Guid.NewGuid().ToString("N")+extension);
                using var input=entry.Open();using var output=File.Create(cover);await input.CopyToAsync(output);
            }
            if(File.Exists(PackagePath)){var history=Path.Combine(root,"drafts","history");Directory.CreateDirectory(history);var archived=Path.Combine(history,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".myvoice");File.Copy(PackagePath,archived);if(metadata!=null)File.WriteAllText(archived+".json",System.Text.Json.JsonSerializer.Serialize(metadata));}
            File.Move(temporary,PackagePath,true);PreparedCoverPath=cover;return manifest;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
