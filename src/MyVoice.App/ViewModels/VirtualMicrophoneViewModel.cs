using System.Linq;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private readonly IVirtualAudioBackend virtualBackend = new VBCableBackend();
    private VirtualAudioConnection virtualConnection = new("VB-Audio Virtual Cable", null, null);
    private string lastVirtualDiagnostics="";
    public string VirtualPlaybackName=>virtualConnection.Playback?.Name??"CABLE Input introuvable";
    public string VirtualCaptureName=>virtualConnection.Capture?.Name??"CABLE Output (VB-Audio Virtual Cable)";
    public bool VirtualMicrophoneMissing => !virtualConnection.Detected;
    public string VirtualMicrophoneStatus => !virtualConnection.Detected ? T["Runtime83"] :
        graph.DeviceStatuses.FirstOrDefault(s => s.Id == "virtual") is { Available: true } && graph.VirtualId == virtualConnection.Playback?.Id
        ? T["Runtime25"] : "Connexion audio en cours…";
    public string VirtualMicrophoneDiagnostics => $"{virtualBackend.Name}\n{VirtualMicrophoneStatus}\nSortie Windows : {virtualConnection.Playback?.Id ?? "absente"}\nEntrée des applications : {virtualConnection.Capture?.Name ?? "absente"}\n{graph.DeviceStatuses.FirstOrDefault(s => s.Id == "virtual")?.Message}\n48 000 Hz · stéréo · WASAPI · tampon demandé 30 ms";
    private void ReconcileVirtualMicrophone()
    {
        virtualConnection = virtualBackend.Resolve(OutputDevices.ToArray(), Devices.ToArray(), Settings.VirtualDeviceId);
        if(mixerStore == null) return;
        var bus = mixerState.Current.Outputs.Single(b => b.Id == "virtual");
        var device = virtualConnection.Detected ? virtualConnection.Playback : null;
        var next = bus with { Name="VB-CABLE · Micro virtuel", DeviceMode=device == null ? "Internal" : "Fixed", DeviceId=device?.Id, DeviceName=device?.Name, Backend="WASAPI", BufferMs=30 };
        Settings.VirtualDeviceId=device?.Id;
        if(bus != next)
        {
            mixerState.Current = mixerState.Current with { Outputs=mixerState.Current.Outputs.Select(b=>b.Id=="virtual"?next:b).ToList() };
            var card=MixerOutputs.FirstOrDefault(b=>b.Id=="virtual");
            if(card!=null){card.ReplaceValue(next);RefreshOutputChoices(card);}
            ApplyMixer(); ScheduleSave();
            store.Log("Virtual microphone routing: " + (device?.Name ?? "backend absent"));
        }
        NotifyVirtualMicrophone();
    }
    private void NotifyVirtualMicrophone()
    {
        var diagnostic=VirtualMicrophoneDiagnostics;if(diagnostic==lastVirtualDiagnostics)return;lastVirtualDiagnostics=diagnostic;
        OnPropertyChanged(nameof(VirtualMicrophoneStatus)); OnPropertyChanged(nameof(VirtualMicrophoneMissing));
        OnPropertyChanged(nameof(VirtualMicrophoneDiagnostics)); OnPropertyChanged(nameof(CableStatus));
        OnPropertyChanged(nameof(VirtualPlaybackName));OnPropertyChanged(nameof(VirtualCaptureName));
    }
    [CommunityToolkit.Mvvm.Input.RelayCommand] private void RestoreCableRouting()
    {
        RefreshCable();
        if(!virtualConnection.Detected){MixerStatus=T["Runtime115"];ShowToast(MixerStatus);return;}
        var profile=mixerState.Current;
        SetMixerProfile(profile with
        {
            Channels=profile.Channels.Select(c=>c.Id is "mic" or "soundboard"?c with{Mute=false,Solo=false,Volume=c.Volume<=0?1:c.Volume}:c with{Solo=false}).ToList(),
            Outputs=profile.Outputs.Select(b=>b.Id=="virtual"?b with{Mute=false,Volume=b.Volume<=0?1:b.Volume}:b).ToList(),
            Routes=profile.Routes.Where(r=>!(r.BusId=="virtual"&&r.ChannelId is "mic" or "soundboard")).Concat(new[]{new AudioRoute("mic","virtual",true),new AudioRoute("soundboard","virtual",true)}).ToList()
        });
        if(automaticAudio)EnsureMicrophone();
        MixerStatus="Envoi micro et Soundboard rétabli vers VB-CABLE. Choisissez « "+VirtualCaptureName+" » comme micro dans votre application.";
        Save();ShowToast(MixerStatus);
    }
}
