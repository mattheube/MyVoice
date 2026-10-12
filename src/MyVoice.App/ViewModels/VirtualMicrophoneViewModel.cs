using System.Linq;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private readonly IVirtualAudioBackend virtualBackend = new VBCableBackend();
    private VirtualAudioConnection virtualConnection = new("VB-Audio Virtual Cable", null, null);
    private string lastVirtualDiagnostics="";
    public bool VirtualMicrophoneMissing => !virtualConnection.Detected;
    public string VirtualMicrophoneStatus => !virtualConnection.Detected ? "Périphérique audio virtuel requis" :
        graph.DeviceStatuses.FirstOrDefault(s => s.Id == "virtual") is { Available: true } && graph.VirtualId == virtualConnection.Playback?.Id
        ? "Connecté ✓" : "Connexion audio en cours…";
    public string VirtualMicrophoneDiagnostics => $"{virtualBackend.Name}\n{VirtualMicrophoneStatus}\nSortie Windows : {virtualConnection.Playback?.Id ?? "absente"}\nEntrée des applications : {virtualConnection.Capture?.Name ?? "absente"}\n{graph.DeviceStatuses.FirstOrDefault(s => s.Id == "virtual")?.Message}\n48 000 Hz · stéréo · WASAPI · tampon demandé 30 ms";
    private void ReconcileVirtualMicrophone()
    {
        virtualConnection = virtualBackend.Resolve(OutputDevices.ToArray(), Devices.ToArray(), Settings.VirtualDeviceId);
        if(mixerStore == null) return;
        var bus = mixerState.Current.Outputs.Single(b => b.Id == "virtual");
        var device = virtualConnection.Detected ? virtualConnection.Playback : null;
        var next = bus with { Name="Virtual Microphone", DeviceMode=device == null ? "Internal" : "Fixed", DeviceId=device?.Id, DeviceName=device?.Name, Backend="WASAPI", BufferMs=30 };
        Settings.VirtualDeviceId=device?.Id;
        if(bus != next)
        {
            mixerState.Current = mixerState.Current with { Outputs=mixerState.Current.Outputs.Select(b=>b.Id=="virtual"?next:b).ToList() };
            LoadMixerCards(mixerState.Current); ApplyMixer(); ScheduleSave();
            store.Log("Virtual microphone routing: " + (device?.Name ?? "backend absent"));
        }
        NotifyVirtualMicrophone();
    }
    private void NotifyVirtualMicrophone()
    {
        var diagnostic=VirtualMicrophoneDiagnostics;if(diagnostic==lastVirtualDiagnostics)return;lastVirtualDiagnostics=diagnostic;
        OnPropertyChanged(nameof(VirtualMicrophoneStatus)); OnPropertyChanged(nameof(VirtualMicrophoneMissing));
        OnPropertyChanged(nameof(VirtualMicrophoneDiagnostics)); OnPropertyChanged(nameof(CableStatus));
    }
}
