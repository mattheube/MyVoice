using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyVoice.Updater;
public static class UpdateTransportTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>action(request,token);}
    private sealed class BrokenStream:MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)=>throw new IOException("network interrupted");
    }
    public static async Task Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"MyVoice-update-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        byte[] binary=[0x4D,0x5A,4,5,6];var hash=Convert.ToHexString(SHA256.HashData(binary));
        var release=new ReleaseManifest("2.2.0","",hash){DownloadUrl="https://test.invalid/MyVoiceSetup.exe",ReleaseUrl="https://test.invalid/releases/v2.2.0"};
        var manifest=JsonSerializer.Serialize(release);var source="https://test.invalid/manifest.json";
        HttpResponseMessage Json()=>new(HttpStatusCode.OK){Content=new StringContent(manifest)};
        using var ok=new UpdateService(new FakeHandler((r,t)=>Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith(".json")?Json():new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(binary)})));
        void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
        async Task Reject(Func<Task> action,string name){try{await action();}catch(Exception e)when(e is InvalidDataException or IOException or HttpRequestException or OperationCanceledException or InvalidOperationException){Console.WriteLine("PASS "+name);return;}throw new Exception("Accepted: "+name);}
        Check(await ok.CheckAsync(source,new Version(2,2,0),CancellationToken.None)==null,"HTTPS no-update response");
        var next=await ok.CheckAsync(source,new Version(2,1,0),CancellationToken.None);Check(next?.Version=="2.2.0","HTTPS newer release discovered");
        var local=await ok.DownloadAsync(next!,source,root,CancellationToken.None);Check(File.ReadAllBytes(local).SequenceEqual(binary),"HTTPS download verified and staged without execution");
        await Reject(()=>ok.DownloadAsync(release with{Sha256=new string('0',64)},source,root,CancellationToken.None),"Incorrect SHA-256 rejected");
        Check(!Directory.GetFiles(root,"*.part").Any(),"Partial file removed after failed verification");
        using var offline=new UpdateService(new FakeHandler((r,t)=>throw new HttpRequestException("offline")));
        await Reject(()=>offline.ReadManifestAsync(source,CancellationToken.None),"Offline update check returns a recoverable error");
        using var interrupted=new UpdateService(new FakeHandler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StreamContent(new BrokenStream())})));
        await Reject(()=>interrupted.DownloadAsync(release,source,root,CancellationToken.None),"Interrupted download is not staged");
        Check(File.ReadAllBytes(local).SequenceEqual(binary)&&!Directory.GetFiles(root,"*.part").Any(),"Interrupted retry preserves last verified file");
        using var insecure=new UpdateService(new FakeHandler((r,t)=>{var response=new HttpResponseMessage(HttpStatusCode.Redirect);response.Headers.Location=new Uri("http://test.invalid/file");return Task.FromResult(response);}));
        await Reject(()=>insecure.ReadManifestAsync(source,CancellationToken.None),"HTTPS downgrade redirect rejected");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();await Reject(()=>ok.ReadManifestAsync(source,cancellation.Token),"Canceled check exits promptly");
        await Reject(()=>ok.DownloadAsync(release with{Version="../escape"},source,root,CancellationToken.None),"Manifest version cannot escape cache directory");
        try{UpdateService.Validate(release,DistributionDefaults.ManifestUrl);throw new Exception("Wrong official origin accepted");}catch(InvalidDataException){Console.WriteLine("PASS Official download must match repository and release tag");}
        var official=release with{DownloadUrl="https://github.com/mattheube/MyVoice/releases/download/v2.2.0/MyVoiceSetup.exe",ReleaseUrl="https://github.com/mattheube/MyVoice/releases/tag/v2.2.0"};Check(UpdateService.Validate(official,DistributionDefaults.ManifestUrl).Installer==official.DownloadUrl,"Official manifest schema accepted");
        var protectedFile=Path.Combine(root,"personal-library.json");await File.WriteAllTextAsync(protectedFile,"{\"voices\":[\"private\"]}");var before=File.ReadAllBytes(protectedFile);await ok.DownloadAsync(release,source,root,CancellationToken.None);Check(File.ReadAllBytes(protectedFile).SequenceEqual(before),"Staging leaves personal catalog unchanged");
    }
}
