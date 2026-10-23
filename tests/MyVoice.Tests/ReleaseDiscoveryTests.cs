using System.Net;
using System.Net.Http;
using System.Text.Json;
using MyVoice.Updater;
public static class ReleaseDiscoveryTests
{
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(respond(request));}}
    public static void Run(Action<string,Action> test)
    {
        test("SemVer orders numeric prereleases, stable and metadata correctly",()=>{
            string[] ordered=["1.0.0-alpha","1.0.0-alpha.1","1.0.0-alpha.beta","1.0.0-beta","1.0.0-beta.2","1.0.0-beta.11","1.0.0-rc.1","1.0.0"];
            for(int i=1;i<ordered.Length;i++)if(!ReleaseVersion.IsNewer("1.0.0",ordered[i],ordered[i-1]))throw new Exception("SemVer ordering");
            if(ReleaseVersion.IsNewer("1.0.0","v1.0.0+build.8","1.0.0+build.1")||ReleaseVersion.IsNewer("1.0.0","v1.0.0-beta.01","1.0.0-beta.1"))throw new Exception("Invalid precedence");
        });
        foreach(var beta in new[]{false,true})test("GitHub "+(beta?"Beta":"Stable")+" discovery skips drafts, incomplete assets and other channels",()=>{
            object Release(string tag,bool pre,bool draft=false,bool complete=true)=>new{tag_name=tag,prerelease=pre,draft,assets=(complete?new[]{"manifest.json","MyVoiceSetup.exe","MyVoice-2.7.0-win-x64.zip"}:new[]{"manifest.json"}).Select(name=>new{name})};
            var expected=beta?"v2.7.0-beta.11":"v2.7.0";
            var source=beta?DistributionDefaults.BetaManifestUrl:DistributionDefaults.ManifestUrl;
            using var service=new UpdateService(new Handler(request=>{
                if(request.RequestUri!.Host=="api.github.com")return Json(new[]{Release("v2.7.0-beta.2",true),Release("v2.7.0-beta.99",true,true),Release("v2.7.0-beta.100",true,false,false),Release("v2.7.0",false),Release("v2.7.0-beta.11",true)});
                if(request.RequestUri.AbsolutePath!=$"/mattheube/MyVoice/releases/download/{expected}/manifest.json")throw new Exception("Used stale catalog or wrong release");
                return Json(new ReleaseManifest("2.7.0","",new string('a',64)){Tag=expected,Channel=beta?"beta":"stable",DownloadUrl=$"https://github.com/mattheube/MyVoice/releases/download/{expected}/MyVoiceSetup.exe",ReleaseUrl=$"https://github.com/mattheube/MyVoice/releases/tag/{expected}",PackageUrl=$"https://github.com/mattheube/MyVoice/releases/download/{expected}/MyVoice-2.7.0-win-x64.zip",PackageSha256=new string('b',64)});
            }));
            if(service.ReadManifestAsync(source,CancellationToken.None).GetAwaiter().GetResult().Tag!=expected)throw new Exception("Wrong release selected");
        });
        static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value))};
    }
}
