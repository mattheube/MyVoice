using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;

namespace MyVoice.AI;

/// <summary>Runs before the model process. Never uses Conversation to repair Conversation.</summary>
public static class EngineBootstrapper
{
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
