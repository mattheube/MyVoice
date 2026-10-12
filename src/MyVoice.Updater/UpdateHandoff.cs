using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace MyVoice.Updater;
public sealed record UpdateJob(string Target,string Package,string Source,ReleaseManifest Release,int ParentId,long ParentStarted,string Token,bool SmokeTest=false);
public sealed record UpdateReceipt(string Status,string Version,string Message,string Backup="");
public static class UpdateHandoff
{
    public static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MyVoice","Updates");
    public static string Transactions=>Path.Combine(Root,"transactions");
    public static string MutexName(string target)=>"Local\\MyVoice.Update."+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.TrimEndingDirectorySeparator(Path.GetFullPath(target)).ToUpperInvariant())))[..24];
    public static bool IsBusy(string target)
    {
        using var mutex=new Mutex(false,MutexName(target));
        try{if(!mutex.WaitOne(0))return true;}catch(AbandonedMutexException){}
        mutex.ReleaseMutex();return false;
    }
    public static string ValidateJobPath(string path)
    {
        path=Path.GetFullPath(path);var directory=Path.GetDirectoryName(path)!;
        if(Path.GetFileName(path)!="job.json"||Directory.GetParent(directory)?.FullName!=Path.GetFullPath(Transactions)||!Guid.TryParseExact(Path.GetFileName(directory),"N",out _))throw new InvalidDataException("Transaction inconnue");
        UpdateTransaction.AssertPlainPath(path);return directory;
    }
    public static async Task LaunchAsync(string target,string package,string source,ReleaseManifest release,CancellationToken token,bool smoke=false)
    {
        if(source!=DistributionDefaults.ManifestUrl&&source!=DistributionDefaults.BetaManifestUrl)throw new InvalidDataException("Source officielle requise");
        UpdateService.Validate(release,source);await UpdateService.VerifyPackageAsync(package,release.PackageSha256,token);
        target=Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));var hostSource=Path.Combine(target,"updater");
        if(!File.Exists(Path.Combine(hostSource,"MyVoice.UpdateHost.exe")))throw new FileNotFoundException("Le composant de mise à jour doit être installé avec cette version de MyVoice.");
        var directory=Path.Combine(Transactions,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var host=Path.Combine(directory,"host");await Task.Run(()=>UpdateTransaction.CopyTree(hostSource,host),token);
        using var current=Process.GetCurrentProcess();
        var job=new UpdateJob(target,Path.GetFullPath(package),source,release,current.Id,current.StartTime.ToUniversalTime().Ticks,Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),smoke);
        var file=Path.Combine(directory,"job.json");File.WriteAllText(file,JsonSerializer.Serialize(job));
        var start=new ProcessStartInfo(Path.Combine(host,"MyVoice.UpdateHost.exe")){UseShellExecute=false,WorkingDirectory=host};start.ArgumentList.Add(file);
        using var helper=Process.Start(start)??throw new IOException("Impossible de lancer la mise à jour");
        var until=DateTime.UtcNow.AddSeconds(25);
        try
        {
            while(DateTime.UtcNow<until)
            {
                token.ThrowIfCancellationRequested();
                if(File.Exists(Path.Combine(directory,"accepted"))&&File.ReadAllText(Path.Combine(directory,"accepted"))==job.Token)return;
                if(ReadReceipt(directory) is {Status:"failed"} receipt)throw new IOException(receipt.Message);
                if(helper.HasExited)throw new IOException("Le programme de mise à jour n’a pas démarré.");
                await Task.Delay(100,token);
            }
        }
        catch{File.WriteAllText(Path.Combine(directory,"cancelled"),"handoff cancelled");throw;}
        // No forced shutdown of the app. The helper cannot apply anything before this process exits.
        File.WriteAllText(Path.Combine(directory,"cancelled"),"startup timeout");
        throw new TimeoutException("La mise à jour n’a pas répondu. MyVoice reste ouvert.");
    }
    public static UpdateReceipt? ReadReceipt(string directory){try{return JsonSerializer.Deserialize<UpdateReceipt>(File.ReadAllText(Path.Combine(directory,"result.json")));}catch{return null;}}
    public static void WriteReceipt(string directory,UpdateReceipt result){var file=Path.Combine(directory,"result.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(result));File.Move(file+".tmp",file,true);}
    public static bool IsBlocked(string tag){try{return File.ReadAllText(Path.Combine(Root,"failed-release.txt"))==tag;}catch{return false;}}
    public static void MarkHealthy(string jobPath,string installed)
    {
        var directory=ValidateJobPath(jobPath);var job=JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(jobPath))??throw new InvalidDataException();
        if(!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)),Path.TrimEndingDirectorySeparator(Path.GetFullPath(job.Target)),StringComparison.OrdinalIgnoreCase)||installed.Split('+')[0]!=job.Release.Tag.TrimStart('v'))throw new InvalidDataException("La version relancée ne correspond pas à la mise à jour");
        File.WriteAllText(Path.Combine(directory,"healthy"),job.Token);
    }
}
