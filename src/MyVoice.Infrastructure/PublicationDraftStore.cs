namespace MyVoice.Infrastructure;
public sealed class PublicationDraftStore(string root)
{
    public string PackagePath=>Path.Combine(root,"drafts","publication.myvoice");
    public async Task<CreationManifest> PrepareAsync(string source)
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
            if(manifest.Sounds.Any(s=>!string.IsNullOrEmpty(s.SourceContentId))||!string.IsNullOrEmpty(manifest.Preset?.SourceContentId))
                throw new InvalidDataException("Ce package contient une création importée. Elle peut être conservée dans votre bibliothèque, mais pas republiée comme votre création.");
            File.Move(temporary,PackagePath,true);return manifest;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
