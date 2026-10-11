using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.Audio;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;

public partial class MainViewModel
{
    public ObservableCollection<AudioDevice> MixerInputs {get;}=[];
    public ObservableCollection<AudioDevice> MixerSystemInputs {get;}=[];
    public ObservableCollection<ApplicationAudioSource> MixerApplications {get;}=[];
    public ObservableCollection<AudioDevice> RecordingSources {get;}=[];
    public string[] MixerSourceTypes {get;}=["Physical / Virtual Input","Application","System Audio"];
    [ObservableProperty] private string mixerSourceType="Physical / Virtual Input";
    [ObservableProperty] private AudioDevice? mixerInput;
    [ObservableProperty] private AudioDevice? mixerSystemInput;
    [ObservableProperty] private ApplicationAudioSource? mixerApplication;
    [ObservableProperty] private string recordingStatus="WAV · 48 kHz · 32-bit float";
    [ObservableProperty] private bool isRecording;
    [ObservableProperty] private AudioDevice? selectedRecordingTarget;
    partial void OnSelectedRecordingTargetChanged(AudioDevice? value){if(value!=null)RecordingSource=value.Id;}
    public string RecordingSource{get=>mixerState.RecordingSource;set{if(!string.IsNullOrEmpty(value)){mixerState.RecordingSource=value;OnPropertyChanged();ScheduleSave();}}}
    public bool IsApplicationSource=>MixerSourceType=="Application";
    public bool IsSystemSource=>MixerSourceType=="System Audio";
    public bool IsDeviceSource=>MixerSourceType=="Physical / Virtual Input";
    partial void OnSelectedMixerChannelChanged(MixerChannelCard? value)
    {
        if(value?.CanRemove!=true)return;
        MixerSourceType=value.Value.Type=="application"?"Application":value.Value.Type=="system"?"System Audio":"Physical / Virtual Input";
        OnPropertyChanged(nameof(HotkeyLabel));
    }
    [RelayCommand] private void RelinkMixerSource()
    {
        if(SelectedMixerChannel is not {CanRemove:true} card)return;var c=card.Value;AudioChannel updated;
        if(c.Type=="application")
        {if(MixerApplication==null){MixerStatus="Choisissez une application active.";return;}updated=c with{ApplicationName=MixerApplication.Name,CaptureEnabled=false};}
        else
        {var d=c.Type=="system"?MixerSystemInput:MixerInput;if(d==null)return;updated=c with{DeviceId=d.Id=="@default"?null:d.Id,DeviceName=d.Name,DeviceMode=d.Id=="@default"?"Default":"Fixed",CaptureEnabled=false};}
        SetMixerProfile(mixerState.Current with{Channels=mixerState.Current.Channels.Select(x=>x.Id==c.Id?updated:x).ToList()});SelectedMixerChannel=MixerChannels.First(x=>x.Id==c.Id);MixerStatus="Source reliée. Son bouton Capture permet de la démarrer.";
    }
    partial void OnMixerSourceTypeChanged(string value){OnPropertyChanged(nameof(IsApplicationSource));OnPropertyChanged(nameof(IsSystemSource));OnPropertyChanged(nameof(IsDeviceSource));}
    private void RefreshMixerSourceChoices()
    {
        MixerInputs.Clear();MixerInputs.Add(new("@default","Microphone Windows par défaut"));foreach(var d in mixerInputCache)MixerInputs.Add(d);
        MixerSystemInputs.Clear();MixerSystemInputs.Add(new("@default","Sortie Windows par défaut"));foreach(var d in mixerOutputCache)MixerSystemInputs.Add(d);
        MixerInput=MixerInputs.FirstOrDefault();MixerSystemInput=MixerSystemInputs.FirstOrDefault();
        MixerApplications.Clear();foreach(var app in mixerApplicationCache)MixerApplications.Add(app);MixerApplication=MixerApplications.FirstOrDefault();
    }
    private void RefreshRecordingSources()
    {
        var selected=RecordingSource;
        RecordingSources.Clear();RecordingSources.Add(new("raw-mic","Microphone avant traitement"));RecordingSources.Add(new("processed-mic","Microphone traité / voix IA"));
        foreach(var b in MixerOutputs)RecordingSources.Add(new(b.Id,b.Name));
        foreach(var c in MixerChannels)RecordingSources.Add(new("channel:"+c.Id,"Canal · "+c.Name));
        SelectedRecordingTarget=RecordingSources.FirstOrDefault(s=>s.Id==selected)??RecordingSources.FirstOrDefault(s=>s.Id=="recording")??RecordingSources.FirstOrDefault();
        OnPropertyChanged(nameof(RecordingSource));
    }
    [RelayCommand] private void AddMixerSource()
    {
        if(MixerChannels.Count>=24){MixerStatus="24 sources maximum.";return;}
        AudioChannel channel;
        if(MixerSourceType=="Application")
        {
            if(!ApplicationAudioCatalog.Supported||MixerApplication==null){MixerStatus="Lancez la lecture audio de l’application puis actualisez. Windows build 20348 minimum.";return;}
            if(MixerChannels.Any(c=>c.Value.Type=="application"&&c.Value.ApplicationName==MixerApplication.Name)){MixerStatus="Cette application est déjà dans le mix.";return;}
            channel=new(){Name=MixerApplication.Name,Type="application",ApplicationName=MixerApplication.Name,CaptureEnabled=false};
        }
        else
        {
            var device=MixerSourceType=="System Audio"?MixerSystemInput:MixerInput;if(device==null)return;
            if(MixerSourceType!="System Audio"&&device.Id==Settings.InputDeviceId){MixerStatus="Ce microphone possède déjà son canal.";return;}
            channel=new(){Name=device.Name,Type=MixerSourceType=="System Audio"?"system":"capture",DeviceId=device.Id=="@default"?null:device.Id,DeviceMode=device.Id=="@default"?"Default":"Fixed",DeviceName=device.Name,CaptureEnabled=false};
        }
        channel=channel with{Name=channel.Name[..Math.Min(80,channel.Name.Length)]};
        // Adding a source only saves its configuration. Its explicit Capture switch starts it.
        var p=mixerState.Current with{Channels=mixerState.Current.Channels.Append(channel).ToList(),Routes=mixerState.Current.Routes.Concat(MixerOutputs.Select(b=>new AudioRoute(channel.Id,b.Id,b.Id is "stream" or "recording" or "master"))).ToList()};
        SetMixerProfile(p);SelectedMixerChannel=MixerChannels.First(c=>c.Id==channel.Id);MixerStatus="Source ajoutée à l’arrêt. Activez Capture sur sa tranche quand vous êtes prêt.";
    }
    [RelayCommand] private void RemoveMixerSource(MixerChannelCard? channel)
    {if(channel?.CanRemove==true)SetMixerProfile(mixerState.Current with{Channels=mixerState.Current.Channels.Where(c=>c.Id!=channel.Id).ToList(),Routes=mixerState.Current.Routes.Where(r=>r.ChannelId!=channel.Id).ToList()});}
    [RelayCommand] private void RequestMixerRecording()
    {
        if(graph.Recorder.Recording)return;
        var dialog=new SaveFileDialog{Title="Enregistrer le mix sélectionné",Filter="WAV 48 kHz / float32|*.wav",FileName="MyVoice-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".wav",OverwritePrompt=true};
        if(dialog.ShowDialog()!=true)return;
        try{graph.Recorder.Start(dialog.FileName,RecordingSource);IsRecording=true;RecordingStatus="● REC · "+Path.GetFileName(dialog.FileName);}
        catch(Exception e){RecordingStatus=e.Message;}
    }
    [RelayCommand] private async Task StopMixerRecordingAsync()
    {await graph.Recorder.StopAsync();IsRecording=false;RecordingStatus=graph.Recorder.Error??"Enregistré · "+graph.Recorder.FilePath;}
    private void TickMixerRecording()
    {
        IsRecording=graph.Recorder.Recording;
        if(IsRecording)RecordingStatus=$"● REC   {TimeSpan.FromSeconds(graph.Recorder.Duration):hh\\:mm\\:ss}   {graph.Recorder.Bytes/1048576d:0.0} Mo";
        else if(graph.Recorder.Error!=null)RecordingStatus=graph.Recorder.Error;
    }
}
