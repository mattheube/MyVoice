using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;

namespace MyVoice.AI;

/// <summary>Runs before the model process. Never uses Conversation to repair Conversation.</summary>
public static class EngineBootstrapper
{
    public sealed record Runtime(string Python,string Repository);
    public static async Task<Runtime> EnsureRuntimeAsync(string python,string repository,string setupScript,Action<string>? log,CancellationToken token,bool force=false,string? managedRoot=null)
    {
        var root=managedRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MyVoice","AI");
        var expectedPython=Path.Combine(root,"venv","Scripts","python.exe");
        if(string.IsNullOrWhiteSpace(python))python=expectedPython;
        if(string.IsNullOrWhiteSpace(repository))repository=Path.Combine(root,"seed-vc");
        await Gate.WaitAsync(token);
        try
        {
            bool healthy=false;
            log?.Invoke("CHECKING · Runtime et dépendances");
            if(!force&&File.Exists(python))
            {
                try
                {
                    ManagedRuntime.RepairLauncher(python);
                    var probe=await Run(python,["-I","-c","import sys,struct,torch,torchaudio,numpy,librosa,soundfile,hydra; assert sys.version_info[:2]==(3,11) and struct.calcsize('P')==8; print('Runtime dependencies verified')"],token);
                    healthy=probe.Code==0&&File.Exists(Path.Combine(repository,"inference.py"))&&File.Exists(Path.Combine(repository,"hf_utils.py"))&&Directory.Exists(Path.Combine(repository,"modules"))&&Directory.Exists(Path.Combine(repository,"configs"));
                    log?.Invoke(probe.Output.Trim());
                }
                catch(Exception e) when(e is IOException or System.ComponentModel.Win32Exception){log?.Invoke("BROKEN · "+e.Message);}
            }
            if(healthy){log?.Invoke("READY · Runtime et backend présents ; modèle à initialiser");return new(python,repository);}
            if(!File.Exists(setupScript))throw new FileNotFoundException("Le programme de réparation est absent du dossier MyVoice.",setupScript);
            log?.Invoke("REPAIRING · Environnement contrôlé ; voix et modèles conservés");
            var info=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-NoProfile","-File",setupScript,"-Root",root})info.ArgumentList.Add(arg);
            foreach(var key in new[]{"PYTHONHOME","PYTHONPATH","__PYVENV_LAUNCHER__","PSModulePath"})info.Environment.Remove(key);
            info.StandardOutputEncoding=System.Text.Encoding.UTF8;info.StandardErrorEncoding=System.Text.Encoding.UTF8;
            using var child=Process.Start(info)??throw new IOException("Impossible de lancer la réparation.");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(45));
            async Task Pump(StreamReader reader){while(await reader.ReadLineAsync(timeout.Token) is {} line)log?.Invoke(line);}
            var stdout=Pump(child.StandardOutput);var stderr=Pump(child.StandardError);
            try{await Task.WhenAll(child.WaitForExitAsync(timeout.Token),stdout,stderr);}
            catch{if(!child.HasExited)child.Kill(true);throw;}
            if(child.ExitCode!=0)throw new IOException("Réparation interrompue ou échouée. Consultez les détails ; fichiers personnels conservés.");
            log?.Invoke("VERIFIED · Dépendances testées ; démarrage du modèle ensuite");
            return new(expectedPython,Path.Combine(root,"seed-vc"));
        }
        finally{Gate.Release();}
    }
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private const string VadTest = "import sys,struct,importlib.metadata; assert sys.version_info[:2]==(3,11) and struct.calcsize('P')==8; import webrtcvad; assert importlib.metadata.version('webrtcvad-wheels')=='2.0.14'; v=webrtcvad.Vad(1); assert not any(v.is_speech(bytes(1920),48000) for _ in range(50)); print('VAD READY: 50 silent frames')";
    private const string Wheel = "webrtcvad_wheels-2.0.14-cp311-cp311-win_amd64.whl";
    private const string Digest = "0D9AD6EF4EF7C89228469DF52A4657886055A9F1AB036EDE09400E2279AC5C70";

    public static async Task EnsureConversationAsync(string python, Action<string>? log, CancellationToken token)
    {
        await Gate.WaitAsync(token);
        try
        {
            log?.Invoke("Vérification du runtime et de WebRTC VAD…");
            ManagedRuntime.RepairLauncher(python);
            var check = await Run(python, ["-I", "-c", VadTest], token);
            if (check.Code == 0) { log?.Invoke(check.Output.Trim()); return; }
            log?.Invoke("VAD à réparer : " + check.Output);
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyVoice", "AI");
            var uv = Path.Combine(root, "tools", "uv.exe");
            if (!File.Exists(uv)) throw new IOException("Outil de réparation absent. Lancez Installer / réparer le moteur pour restaurer le runtime.");
            var cache = Path.Combine(root, "packages"); Directory.CreateDirectory(cache);
            var wheel = Path.Combine(cache, Wheel);
            if (!Valid(wheel))
            {
                log?.Invoke("Téléchargement de WebRTC VAD 2.0.14…");
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
                var bytes = await http.GetByteArrayAsync("https://files.pythonhosted.org/packages/80/bd/4309d83120812d04af99aa21a976a4bb8cc10e39f709232c0180bd4e0146/" + Wheel, token);
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(Digest, StringComparison.Ordinal)) throw new IOException("Empreinte VAD invalide.");
                await File.WriteAllBytesAsync(wheel + ".tmp", bytes, token); File.Move(wheel + ".tmp", wheel, true);
            }
            log?.Invoke("Installation du VAD dans le runtime sélectionné…");
            var install = await Run(uv, ["pip", "install", "--python", python, "--no-deps", "--no-index", "--reinstall", wheel], token);
            log?.Invoke(install.Output);
            if (install.Code != 0) throw new IOException("Installation VAD échouée : " + install.Output);
            check = await Run(python, ["-I", "-c", VadTest], token);
            if (check.Code != 0) throw new IOException("Auto-test VAD échoué : " + check.Output);
            log?.Invoke(check.Output.Trim());
        }
        finally { Gate.Release(); }
    }

    private static bool Valid(string path) => File.Exists(path) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == Digest;

    private static async Task<(int Code, string Output)> Run(string file, string[] args, CancellationToken token)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var key in new[] { "PYTHONHOME", "PYTHONPATH", "__PYVENV_LAUNCHER__" }) info.Environment.Remove(key);
        info.Environment["PYTHONUTF8"]="1";
        using var child = Process.Start(info) ?? throw new IOException("Impossible de lancer le runtime.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var output=child.StandardOutput.ReadToEndAsync(token); var errors=child.StandardError.ReadToEndAsync(token);
        try { await child.WaitForExitAsync(timeout.Token); }
        catch { if (!child.HasExited) child.Kill(true); throw; }
        return (child.ExitCode, (await output) + (await errors));
    }
}
