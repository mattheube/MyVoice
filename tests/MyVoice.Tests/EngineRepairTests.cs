using System.Diagnostics;
using MyVoice.AI;

internal static class EngineRepairTests
{
    public static async Task Full(string root,string script)
    {
        if(Directory.Exists(Path.Combine(root,"venv")))throw new Exception("Use a fresh isolated directory");
        Directory.CreateDirectory(Path.Combine(root,"models"));var sentinel=Path.Combine(root,"models","preserved.txt");File.WriteAllText(sentinel,"preserved");
        var runtime=await EngineBootstrapper.EnsureRuntimeAsync("","",script,Console.WriteLine,CancellationToken.None,managedRoot:root);
        await EngineBootstrapper.EnsureConversationAsync(runtime.Python,Console.WriteLine,CancellationToken.None);
        var lines=new List<string>();await EngineBootstrapper.EnsureRuntimeAsync(runtime.Python,runtime.Repository,script,lines.Add,CancellationToken.None,managedRoot:root);
        if(lines.Any(l=>l.StartsWith("REPAIRING"))||File.ReadAllText(sentinel)!="preserved")throw new Exception("Healthy restart reinstalled or model marker changed");
        Console.WriteLine("PASS full bootstrap from no runtime, dependency/VAD test, healthy restart and model preservation");
    }
    public static async Task Run(string root)
    {
        var managed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MyVoice","AI");
        var python=Path.Combine(managed,"python","cpython-3.11.15-windows-x86_64-none","python.exe");
        Directory.CreateDirectory(root);
        var start=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true};
        foreach(var arg in new[]{"-m","venv","--without-pip",Path.Combine(root,"venv")})start.ArgumentList.Add(arg);
        using(var child=Process.Start(start)!){await child.WaitForExitAsync();if(child.ExitCode!=0)throw new Exception("Isolated environment creation failed");}
        var isolated=Path.Combine(root,"venv","Scripts","python.exe");
        // No AI modules or model in this environment: repair cannot accidentally depend on them.
        var marker=Path.Combine(root,"preserved-voice.txt");File.WriteAllText(marker,"preserved");
        await EngineBootstrapper.EnsureConversationAsync(isolated,Console.WriteLine,CancellationToken.None);
        await EngineBootstrapper.EnsureConversationAsync(isolated,Console.WriteLine,CancellationToken.None);
        if(File.ReadAllText(marker)!="preserved")throw new Exception("Repair changed user content");
        Console.WriteLine("PASS missing VAD repaired without model, second startup self-test, content preserved");
    }
}
