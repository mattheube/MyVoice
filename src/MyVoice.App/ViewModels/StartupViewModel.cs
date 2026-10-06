using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private bool automaticAudio, refreshingDevices, resumeAiAfterReconnect;
    private DateTime recoveryAt = DateTime.MinValue;
    [ObservableProperty] private double processedMeter;
    [ObservableProperty] private double monitorMeter;
    public void StartServices()
    {
        automaticAudio = true;
        Refresh(); RefreshOutputs(); EnsureMicrophone();
        if (Settings.AutoStartAi && File.Exists(AiPython)) _ = StartAiCommand.ExecuteAsync(null);
    }
    private void EnsureMicrophone()
    {
        if (lifetime.IsCancellationRequested || Running || SelectedDevice == null || Calibrating) return;
        try { mic.Start(SelectedDevice.Id); Running = mic.IsRunning; store.Log("Automatic input: " + SelectedDevice.Name); }
        catch (Exception e) { store.Log("Input retry: " + e.Message); Status = T["Disconnected"]; }
    }
    private void TickRecovery()
    {
        if (!automaticAudio || DateTime.UtcNow < recoveryAt) return;
        recoveryAt = DateTime.UtcNow.AddSeconds(5);
        Refresh(); RefreshOutputs(); EnsureMicrophone();
        RecoverAi();
        if(resumeAiAfterReconnect&&Running&&AiReady&&!AiBusy&&!aiManualStop){resumeAiAfterReconnect=false;_=UseAiVoiceCommand.ExecuteAsync(SelectedAiVoice);}
    }
    [RelayCommand] private void TestVirtualOutput()
    {
        if (graph.VirtualId == null) { Status = T["RouteMissing"]; return; }
        graph.TestVirtualOutput(); Status = T["VirtualTestPlaying"];
        store.Log("Virtual output test: " + VirtualDevice?.Name);
    }
    private void RecoverAi()
    {
        if(aiManualStop||AiBusy||AiReady||!Settings.AutoStartAi||!File.Exists(AiPython)||DateTime.UtcNow<nextAiRetry||aiRestarts>=2)return;
        aiRestarts++;nextAiRetry=DateTime.UtcNow.AddSeconds(30*aiRestarts);Status=T["AiRestarting"];
        _=StartAiCommand.ExecuteAsync(null);
    }
}
