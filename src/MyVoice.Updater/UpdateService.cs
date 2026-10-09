using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace MyVoice.Updater;
public sealed record ReleaseManifest(string Version,string Installer="",string Sha256="")
{
    public string Tag {get;init;}="";
    public string DownloadUrl {get;init;}="";
    public string ReleaseUrl {get;init;}="";
    public string Channel {get;init;}="stable";
    public string MinimumVersion {get;init;}="0.0.0";
    public bool Mandatory {get;init;}
}
public sealed record DownloadProgress(long Received,long? Total);
public static class DistributionDefaults
{
    public const string Repository="mattheube/MyVoice";
    public const string BetaManifestUrl="https://mattheube.github.io/MyVoice/updates/beta.json";
    public const string ManifestUrl="https://github.com/"+Repository+"/releases/latest/download/manifest.json";
}
/// <summary>Stages verified release files. Does not install or execute downloaded content.</summary>
public sealed class UpdateService : IDisposable
{
    private readonly HttpClient http;
    private const long MaxDownload=1024L*1024*1024;
    public UpdateService(HttpMessageHandler? handler=null)
    {
        http=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MyVoice-Updater/2.2");
    }
    private async Task<HttpResponseMessage> GetAsync(Uri uri,CancellationToken token)
    {
        for(int i=0;i<6;i++)
        {
            if(uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("HTTPS requis pour les mises à jour.");
            var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
            if((int)response.StatusCode>=300&&(int)response.StatusCode<400)
            {
                var location=response.Headers.Location;response.Dispose();
                if(location==null)throw new InvalidDataException("Redirection sans destination");
                uri=location.IsAbsoluteUri?location:new Uri(uri,location);continue;
            }
            response.EnsureSuccessStatusCode();return response;
        }
        throw new InvalidDataException("Trop de redirections");
    }
    public static ReleaseManifest Validate(ReleaseManifest release,string source)
    {
        if(!System.Version.TryParse(release.Version,out _)||!Regex.IsMatch(release.Version,@"^\d+\.\d+\.\d+(\.\d+)?$")||!Regex.IsMatch(release.Sha256??"","^[a-fA-F0-9]{64}$"))throw new InvalidDataException("Manifeste de version invalide");
        if(release.Channel is not ("stable" or "beta")||!System.Version.TryParse(release.MinimumVersion,out _))throw new InvalidDataException("Canal ou version minimale invalide");
        release=release with{Installer=string.IsNullOrWhiteSpace(release.DownloadUrl)?release.Installer:release.DownloadUrl};
        if(string.IsNullOrWhiteSpace(release.Installer))throw new InvalidDataException("Fichier de mise à jour absent");
        if(source==DistributionDefaults.ManifestUrl||source==DistributionDefaults.BetaManifestUrl)
        {
            var prefix="https://github.com/"+DistributionDefaults.Repository+"/releases/";
            var tag=string.IsNullOrEmpty(release.Tag)?"v"+release.Version:release.Tag;
            var beta=source==DistributionDefaults.BetaManifestUrl;
            if(release.Channel!=(beta?"beta":"stable")||!(beta?Regex.IsMatch(tag,"^v"+Regex.Escape(release.Version)+@"-beta\.[1-9][0-9]*$"):tag=="v"+release.Version))throw new InvalidDataException("Canal de release incohérent");
            if(release.Installer!=prefix+"download/"+tag+"/MyVoiceSetup.exe"||release.ReleaseUrl!=prefix+"tag/"+tag)throw new InvalidDataException("La version ne correspond pas à la release officielle");
        }
        return release;
    }
    public async Task<ReleaseManifest> ReadManifestAsync(string source,CancellationToken token)
    {
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(token);limit.CancelAfter(TimeSpan.FromSeconds(5));
        string json;
        if(Uri.TryCreate(source,UriKind.Absolute,out var uri)&&uri.Scheme=="https")
        {
            using var response=await GetAsync(uri,limit.Token);using var input=await response.Content.ReadAsStreamAsync(limit.Token);using var output=new MemoryStream();
            var buffer=new byte[8192];int n;while((n=await input.ReadAsync(buffer,limit.Token))>0){if(output.Length+n>65536)throw new InvalidDataException("Manifeste trop volumineux");output.Write(buffer,0,n);}json=System.Text.Encoding.UTF8.GetString(output.ToArray());
        }
        else if(File.Exists(source))
        {
            if(new FileInfo(source).Length>65536)throw new InvalidDataException("Manifeste trop volumineux");json=await File.ReadAllTextAsync(source,limit.Token);
        }
        else throw new InvalidOperationException("Utilisez une adresse HTTPS ou un manifeste local existant.");
        var release=JsonSerializer.Deserialize<ReleaseManifest>(json.TrimStart('\uFEFF'),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Manifeste vide");
        return Validate(release,source);
    }
    public async Task<ReleaseManifest?> CheckAsync(string source,Version current,CancellationToken token)
    {
        var release=await ReadManifestAsync(source,token);
        if(current<Version.Parse(release.MinimumVersion))throw new InvalidDataException("Cette mise à jour nécessite une version intermédiaire : "+release.MinimumVersion);
        return Version.Parse(release.Version)>current?release:null;
    }
    public async Task<string> DownloadAsync(ReleaseManifest release,string source,string cache,CancellationToken token,IProgress<double>? progress=null,IProgress<DownloadProgress>? bytes=null)
    {
        release=Validate(release,source);Directory.CreateDirectory(cache);
        var dest=Path.Combine(cache,"MyVoiceSetup-"+release.Version+".exe");var temp=dest+"."+Guid.NewGuid().ToString("N")+".part";
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(token);limit.CancelAfter(TimeSpan.FromMinutes(10));token=limit.Token;
        try
        {
            if(Uri.TryCreate(release.Installer,UriKind.Absolute,out var uri)&&uri.Scheme=="https")
            {
                using var response=await GetAsync(uri,token);long? total=response.Content.Headers.ContentLength;
                if(total>MaxDownload)throw new InvalidDataException("Mise à jour trop volumineuse");
                using var input=await response.Content.ReadAsStreamAsync(token);await using var output=File.Create(temp);
                var buffer=new byte[65536];long received=0;int count;
                while(true)
                {
                    using var stall=CancellationTokenSource.CreateLinkedTokenSource(token);stall.CancelAfter(TimeSpan.FromSeconds(8));
                    count=await input.ReadAsync(buffer,stall.Token);if(count==0)break;
                    received+=count;if(received>MaxDownload)throw new InvalidDataException("Mise à jour trop volumineuse");
                    await output.WriteAsync(buffer.AsMemory(0,count),token);bytes?.Report(new(received,total));if(total>0)progress?.Report(received/(double)total);
                }
                if(total.HasValue&&received!=total)throw new InvalidDataException("Téléchargement interrompu");
            }
            else if(File.Exists(source))
            {
                var directory=Path.GetDirectoryName(Path.GetFullPath(source))!;var local=Path.GetFullPath(Path.Combine(directory,release.Installer));
                if(!local.StartsWith(directory+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Le fichier doit rester dans le dossier de release local");
                if(new FileInfo(local).Length>MaxDownload)throw new InvalidDataException("Mise à jour trop volumineuse");File.Copy(local,temp,true);
            }
            else throw new InvalidDataException("Le téléchargement doit utiliser HTTPS");
            await VerifyAsync(temp,release.Sha256,token);File.Move(temp,dest,true);progress?.Report(1);return dest;
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static async Task VerifyAsync(string file,string expected,CancellationToken token)
    {
        await using var stream=File.OpenRead(file);var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));
        if(!hash.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Échec de vérification de la mise à jour : SHA-256 différent.");
        stream.Position=0;if(stream.ReadByte()!=0x4D||stream.ReadByte()!=0x5A)throw new InvalidDataException("Installateur Windows invalide");
    }
    public void Dispose()=>http.Dispose();
}
