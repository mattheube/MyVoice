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
        StopLive();
        ai.Stop();
        AiReady = false;
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
            AiLog = "Vérification et réparation du moteur…" + Environment.NewLine;
            async Task Pump(System.IO.StreamReader reader)
            {
                while (await reader.ReadLineAsync(lifetime.Token) is { } line)
                {
                    store.Log("AI install: " + line);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { AiLog += line + Environment.NewLine; if(AiLog.Length>12000)AiLog=AiLog[^12000..]; });
                }
            }
            var logs = Pump(child.StandardError);
            var output = Pump(child.StandardOutput);
            await child.WaitForExitAsync(lifetime.Token);
            await Task.WhenAll(logs, output);
            if (child.ExitCode != 0)
                throw new IOException("AI setup failed. Open logs for details.");
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyVoice", "AI", "runtime.json");
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            AiPython = json.RootElement.GetProperty("Python").GetString()!;
            AiRepository = json.RootElement.GetProperty("Repository").GetString()!;
            await MyVoice.AI.EngineBootstrapper.EnsureConversationAsync(AiPython, line => { store.Log("AI repair: " + line); System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => AiLog += line + Environment.NewLine)); }, lifetime.Token);
            AiState = T["AiInstalled"];
            Save();
        }
        catch (Exception e) { StudioError(e.Message); AiState = "Error"; }
        finally { installerProcess?.Dispose(); installerProcess = null; AiBusy = false; }
        if (AiState == T["AiInstalled"]) await StartAiAsync();
    }
}
