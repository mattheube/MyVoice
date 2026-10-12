using MyVoice.Updater;
using System.Text.Json;
using System.Security.Cryptography;
public static class UpdateSessionTests
{
    private sealed class Reports: IProgress<UpdateSnapshot>{public List<UpdateSnapshot> Values=[];public void Report(UpdateSnapshot value)=>Values.Add(value);}
    public static async Task Run()
    {
        var dir=Path.Combine(Path.GetTempPath(),"MyVoice-session-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        var file=Path.Combine(dir,"setup.exe");await File.WriteAllBytesAsync(file,[77,90,0,1,2]);var manifest=Path.Combine(dir,"manifest.json");
        var release=new ReleaseManifest("3.0.0","setup.exe",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
        await File.WriteAllTextAsync(manifest,JsonSerializer.Serialize(release));
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
        var reports=new Reports();var session=new UpdateSession();await session.RunAsync(manifest,"2.4.0-beta.1",Path.Combine(dir,"cache"),reports,_=>{},CancellationToken.None);
        Check(session.State.Phase==UpdatePhase.Ready&&File.Exists(session.DownloadedFile),"Update session stages verified update without executing it");
        Check(reports.Values.Select(x=>x.Phase).SequenceEqual(new[]{UpdatePhase.Checking,UpdatePhase.Downloading,UpdatePhase.Verifying,UpdatePhase.Ready}),"Update states follow actual download and verification");
        var current=new UpdateSession();await current.RunAsync(manifest,"3.0.0",dir,new Reports(),_=>{},CancellationToken.None);Check(current.State.Phase==UpdatePhase.Current&&current.DownloadedFile==null,"Current version never downloads");
        await File.WriteAllTextAsync(manifest,JsonSerializer.Serialize(release with {Sha256=new string('0',64)}));
        var bad=new UpdateSession();await bad.RunAsync(manifest,"2.4.0",dir,new Reports(),_=>{},CancellationToken.None);Check(bad.State.Phase==UpdatePhase.Failed&&bad.DownloadedFile==null,"Failed verification offers current version without installing");
        using var cancel=new CancellationTokenSource();cancel.Cancel();var skipped=new UpdateSession();await skipped.RunAsync(manifest,"2.4.0",dir,new Reports(),_=>{},cancel.Token);Check(skipped.State.Phase==UpdatePhase.Cancelled,"Startup skip exits update workflow");
    }
}
