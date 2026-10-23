using System.Diagnostics;
using System.Text.Json;
namespace MyVoice.AI;
public static class TextVoiceBackend
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MyVoice","TextVoice");
    public static string Python => Path.Combine(Root,"venv","Scripts","python.exe");
    public static bool Installed => File.Exists(Python)&&File.Exists(Path.Combine(Root,"ready-0.1.7"));
    public static async Task EnsureAsync(string existingPython,Action<string> progress,CancellationToken token)
    {
        if(Installed)
        {
            try{await Run(Python,["-c","import importlib.metadata; from chatterbox.mtl_tts import ChatterboxMultilingualTTS; assert importlib.metadata.version('chatterbox-tts')=='0.1.7'"],progress,token);return;}
            catch(Exception e) when(e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception){progress("REPAIRING · Text to Voice");}
        }
        await InstallAsync(existingPython,progress,token);
    }
    public static async Task InstallAsync(string existingPython,Action<string> progress,CancellationToken token)
    {
        Directory.CreateDirectory(Root);
        progress("Creating the separate Text to Voice environment…");
        if(!File.Exists(Python))await Run(existingPython,["-m","venv",Path.Combine(Root,"venv")],progress,token);
        var gpu=await Run(existingPython,["-c","import torch; print('CUDA_AVAILABLE' if torch.cuda.is_available() else 'CPU_ONLY')"],_=>{},token);
        if(gpu.Contains("CUDA_AVAILABLE"))await Run(Python,["-m","pip","install","torch==2.6.0","torchaudio==2.6.0","--index-url","https://download.pytorch.org/whl/cu124"],progress,token);
        await Run(Python,["-m","pip","install","-r",Path.Combine(AppContext.BaseDirectory,"voice-engine","requirements-tts-lock.txt")],progress,token);
        await Run(Python,["-c","from chatterbox.mtl_tts import ChatterboxMultilingualTTS; print('Text to Voice ready')"],progress,token);
        await File.WriteAllTextAsync(Path.Combine(Root,"ready-0.1.7"),"chatterbox-tts==0.1.7",token);
    }
    public static async Task GenerateAsync(string script,string text,string language,string reference,string output,string device,int threads,double expression,Action<string> progress,CancellationToken token)
    {
        if(!Installed)throw new InvalidOperationException("Install Text to Voice first.");
        var request=output+".request.json";
        try{await File.WriteAllTextAsync(request,JsonSerializer.Serialize(new{text,language,reference,output,device,threads,expression}),token);await Run(Python,["-u",script,"--request",request],progress,token);if(!File.Exists(output))throw new IOException("The model did not create an audio result.");}
        finally{if(File.Exists(request))File.Delete(request);}
    }
    private static async Task<string> Run(string executable,string[] arguments,Action<string> progress,CancellationToken token)
    {
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in arguments)start.ArgumentList.Add(argument);
        start.Environment["PYTHONUTF8"]="1";start.Environment["HF_HUB_DISABLE_TELEMETRY"]="1";
        start.Environment.Remove("PYTHONHOME");start.Environment.Remove("PYTHONPATH");
        using var process=Process.Start(start)??throw new IOException("Cannot start Text to Voice.");
        async Task<string> ReadOutput(){var text=new System.Text.StringBuilder();while(await process.StandardOutput.ReadLineAsync(token) is {} line){text.AppendLine(line);if(text.Length>16000)text.Remove(0,text.Length-16000);if(!string.IsNullOrWhiteSpace(line))progress(line.Length>300?line[^300..]:line);}return text.ToString();}
        var output=ReadOutput();var errors=process.StandardError.ReadToEndAsync(token);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(30));
        try{await process.WaitForExitAsync(timeout.Token);var text=await output;var error=await errors;if(process.ExitCode!=0)throw new InvalidOperationException(error.Length>1200?error[^1200..]:error);progress(text.Length>300?text[^300..]:text);return text;}
        catch{if(!process.HasExited)process.Kill(true);throw;}
    }
}
