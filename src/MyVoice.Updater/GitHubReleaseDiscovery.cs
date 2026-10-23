using System.Text.Json;
namespace MyVoice.Updater;
public sealed partial class UpdateService
{
    public Task<ReleaseManifest> ReadManifestAsync(string source,CancellationToken token)=>
        source==DistributionDefaults.ManifestUrl||source==DistributionDefaults.BetaManifestUrl
        ?ReadGitHubReleaseAsync(source,token):ReadManifestDocumentAsync(source,token);

    private async Task<ReleaseManifest> ReadGitHubReleaseAsync(string source,CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(15));token=deadline.Token;
        var beta=source==DistributionDefaults.BetaManifestUrl;
        (string Tag,SemanticRelease Version)? newest=null;
        for(var page=1;page<=5;page++)
        {
            using var response=await GetAsync(new Uri($"https://api.github.com/repos/{DistributionDefaults.Repository}/releases?per_page=100&page={page}"),token);
            await using var input=await response.Content.ReadAsStreamAsync(token);using var buffer=new MemoryStream();
            var bytes=new byte[16384];int count;
            while((count=await input.ReadAsync(bytes,token))>0){if(buffer.Length+count>4*1024*1024)throw new InvalidDataException("Catalogue GitHub trop volumineux");buffer.Write(bytes,0,count);}
            using var document=JsonDocument.Parse(buffer.ToArray());var releases=document.RootElement;
            foreach(var release in releases.EnumerateArray())
            {
                if(release.GetProperty("draft").GetBoolean()||release.GetProperty("prerelease").GetBoolean()!=beta)continue;
                var tag=release.GetProperty("tag_name").GetString()??"";
                if(!SemanticRelease.TryParse(tag,out var version)||!tag.StartsWith('v'))continue;
                if(beta?(version.Prerelease.Length!=2||version.Prerelease[0]!="beta"||!version.Prerelease[1].All(char.IsAsciiDigit)):version.Prerelease.Length!=0)continue;
                var names=release.GetProperty("assets").EnumerateArray().Select(a=>a.GetProperty("name").GetString()).ToHashSet();
                if(!names.Contains("manifest.json")||!names.Contains("MyVoiceSetup.exe")||!names.Contains($"MyVoice-{version.Major}.{version.Minor}.{version.Patch}-win-x64.zip"))continue;
                if(newest==null||version.CompareTo(newest.Value.Version)>0)newest=(tag,version);
            }
            if(releases.GetArrayLength()<100)break;
        }
        if(newest==null)throw new InvalidDataException(beta?"Aucune Beta complète disponible.":"Aucune version Stable complète disponible.");
        var manifest=await ReadManifestDocumentAsync($"https://github.com/{DistributionDefaults.Repository}/releases/download/{newest.Value.Tag}/manifest.json",token,source);
        if(manifest.Tag!=newest.Value.Tag||string.IsNullOrEmpty(manifest.PackageUrl))throw new InvalidDataException("Le manifeste ne correspond pas à la release sélectionnée.");
        return manifest;
    }
}
