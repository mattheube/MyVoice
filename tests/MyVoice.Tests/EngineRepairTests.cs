using System.Diagnostics;
using MyVoice.AI;

internal static class EngineRepairTests
{
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
