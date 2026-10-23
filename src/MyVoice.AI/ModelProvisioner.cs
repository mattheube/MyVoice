using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace MyVoice.AI;
public static class ModelProvisioner
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static string EngineFolder=>Path.Combine(AppContext.BaseDirectory,"voice-engine");
    private static string StatePath(string group)=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MyVoice","AI","models-"+group+".json");
    public static bool Complete
    {
        get
        {
            try
            {
                if(!TextVoiceBackend.Installed)return false;
                using var receipt=JsonDocument.Parse(File.ReadAllText(StatePath("all")));
                var digest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(EngineFolder,"models.lock.json"))));
                if(!digest.Equals(receipt.RootElement.GetProperty("catalog_sha256").GetString(),StringComparison.OrdinalIgnoreCase))return false;
                foreach(var entry in receipt.RootElement.GetProperty("files").EnumerateObject())
                    if(!File.Exists(entry.Name)||new FileInfo(entry.Name).Length!=entry.Value[0].GetInt64())return false;
                return receipt.RootElement.GetProperty("files").EnumerateObject().Any();
            }
            catch(Exception e) when(e is IOException or JsonException or InvalidOperationException or KeyNotFoundException){return false;}
        }
    }
    public static async Task EnsureAsync(string python,string repository,string group,Action<string>? progress,CancellationToken token,bool verify=false)
    {
        if(group is not ("all" or "seed" or "tts"))throw new ArgumentException("Unsupported engine group",nameof(group));
        await Gate.WaitAsync(token);
        try
        {
            var info=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            info.StandardOutputEncoding=System.Text.Encoding.UTF8;info.StandardErrorEncoding=System.Text.Encoding.UTF8;
            foreach(var arg in new[]{"-u",Path.Combine(EngineFolder,"model_assets.py"),"--repository",repository,"--state",StatePath(group),"--group",group})info.ArgumentList.Add(arg);
            if(verify)info.ArgumentList.Add("--verify");
            foreach(var key in new[]{"PYTHONHOME","PYTHONPATH","__PYVENV_LAUNCHER__"})info.Environment.Remove(key);
            info.Environment["PYTHONUTF8"]="1";info.Environment["HF_HUB_DISABLE_TELEMETRY"]="1";
            using var child=Process.Start(info)??throw new IOException("Impossible de préparer les modèles.");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(60));
            var error=child.StandardError.ReadToEndAsync(timeout.Token);
            async Task Pump(){while(await child.StandardOutput.ReadLineAsync(timeout.Token) is {} line)progress?.Invoke(line);}
            try
            {
                await Task.WhenAll(child.WaitForExitAsync(timeout.Token),Pump());
                var details=await error;
                if(child.ExitCode!=0)throw new IOException("Préparation des modèles interrompue. "+(details.Length>1200?details[^1200..]:details));
            }
            catch{if(!child.HasExited)child.Kill(true);throw;}
        }
        finally{Gate.Release();}
    }
}
