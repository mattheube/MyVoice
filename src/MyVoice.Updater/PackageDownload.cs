using System.Security.Cryptography;
namespace MyVoice.Updater;
public sealed partial class UpdateService
{
    public async Task<string> DownloadPackageAsync(ReleaseManifest release,string source,string cache,CancellationToken token,IProgress<DownloadProgress>? progress=null,Action? verifying=null)
    {
        Validate(release,source);
        if(source!=DistributionDefaults.ManifestUrl&&source!=DistributionDefaults.BetaManifestUrl)throw new InvalidDataException("Source officielle requise");
        if(string.IsNullOrEmpty(release.PackageUrl))throw new InvalidDataException("Package automatique absent");
        Directory.CreateDirectory(cache);var target=Path.Combine(cache,"MyVoice-"+release.Tag+".zip");var temp=target+"."+Guid.NewGuid().ToString("N")+".part";
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(10));token=deadline.Token;
        try
        {
            using var response=await GetAsync(new Uri(release.PackageUrl),token);var total=response.Content.Headers.ContentLength;
            if(total>MaxDownload)throw new InvalidDataException("Package trop volumineux");
            await using(var input=await response.Content.ReadAsStreamAsync(token))
            await using(var output=File.Create(temp))
            {
                var buffer=new byte[131072];long received=0;
                while(true)
                {
                    using var stall=CancellationTokenSource.CreateLinkedTokenSource(token);stall.CancelAfter(TimeSpan.FromSeconds(8));
                    var n=await input.ReadAsync(buffer,stall.Token);if(n==0)break;
                    received+=n;if(received>MaxDownload)throw new InvalidDataException("Package trop volumineux");
                    await output.WriteAsync(buffer.AsMemory(0,n),token);progress?.Report(new(received,total));
                }
                if(total.HasValue&&received!=total)throw new InvalidDataException("Téléchargement incomplet");
            }
            verifying?.Invoke();await VerifyPackageAsync(temp,release.PackageSha256,token);
            File.Move(temp,target,true);return target;
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static async Task VerifyPackageAsync(string path,string digest,CancellationToken token)
    {
        await using var file=File.OpenRead(path);await VerifyPackageAsync(file,digest,token);
    }
    internal static async Task VerifyPackageAsync(Stream file,string digest,CancellationToken token)
    {
        if(!Convert.ToHexString(await SHA256.HashDataAsync(file,token)).Equals(digest,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Empreinte SHA-256 du package incorrecte");
        file.Position=0;if(file.ReadByte()!=80||file.ReadByte()!=75)throw new InvalidDataException("Archive de mise à jour invalide");file.Position=0;
    }
}
