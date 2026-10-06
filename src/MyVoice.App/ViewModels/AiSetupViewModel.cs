using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private Process? installerProcess;
    [RelayCommand]
    private async Task InstallAiRuntimeAsync()
    {
        if (AiBusy)
            return;
        AiBusy = true;
        AiState = T["AiInstalling"];
        try
        {
            var script = Path.Combine(AppContext.BaseDirectory, "voice-engine", "SetupEngine.ps1");
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoProfile", "-File", script })
                start.ArgumentList.Add(arg);
            installerProcess = new Process { StartInfo = start };
            installerProcess.Start();
            var child = installerProcess;
            var logs = Task.Run(async () => { while (await child.StandardError.ReadLineAsync() is { } line) { store.Log("AI install: " + line); } });
            var output = await child.StandardOutput.ReadToEndAsync(lifetime.Token);
            await child.WaitForExitAsync(lifetime.Token);
            await logs;
            if (child.ExitCode != 0)
                throw new IOException("AI setup failed. Open logs for details.");
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyVoice", "AI", "runtime.json");
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            AiPython = json.RootElement.GetProperty("Python").GetString()!;
            AiRepository = json.RootElement.GetProperty("Repository").GetString()!;
            AiState = T["AiInstalled"];
            AiLog = output;
            Save();
        }
        catch (Exception e) { StudioError(e.Message); AiState = "Error"; }
        finally { installerProcess?.Dispose(); installerProcess = null; AiBusy = false; }
    }
}
