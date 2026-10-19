using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.Infrastructure;
namespace MyVoice.App.ViewModels;

public partial class MainViewModel
{
    private MixerStore? mixerStore;
    private MixerState mixerState=new();
    private bool mixerLoading;
    private int mixerTicks;
    public ObservableCollection<MixerChannelCard> MixerChannels {get;}=[];
    public ObservableCollection<MixerOutputCard> MixerOutputs {get;}=[];
    public ObservableCollection<MixerRouteRow> MixerRows {get;}=[];
    public ObservableCollection<AudioProfile> AudioProfiles {get;}=[];
    private AudioDevice[] mixerInputCache=[],mixerOutputCache=[],mixerMmeCache=[];
    private ApplicationAudioSource[] mixerApplicationCache=[];
    private string[] mixerAsioCache=[];
    private int mixerRefreshVersion;
    public string[] MixerBackends=>mixerAsioCache.Length>0?["WASAPI","MME","ASIO"]:["WASAPI","MME"];
    public int[] MixerBuffers {get;}=[10,20,30,50,100,200];
    [ObservableProperty] private MixerChannelCard? selectedMixerChannel;
    [ObservableProperty] private MixerOutputCard? selectedMixerOutput;
    [ObservableProperty] private AudioProfile? selectedAudioProfile;
    [ObservableProperty] private string mixerProfileName="Mon mix";
    [ObservableProperty] private string mixerStatus="48 kHz · float32 · un moteur audio";
    [ObservableProperty] private string mixerWarnings="";
    [ObservableProperty] private string mixerDiagnostics="";
    public bool MixerAdvanced{get=>mixerState.Advanced;set{mixerState.Advanced=value;OnPropertyChanged();ScheduleSave();}}

    private void InitializeMixer()
    {
        mixerStore=new(store.Root);mixerState=mixerStore.Load(Settings);
        mixerState.Current=mixerState.Current with{Channels=mixerState.Current.Channels.Select(c=>c.Id=="mic"?c with{DeviceId=Settings.InputDeviceId,DeviceName=SelectedDevice?.Name}:c).ToList()};
        foreach(var profile in mixerState.Profiles)AudioProfiles.Add(profile);
        LoadMixerCards(mixerState.Current);RefreshMixerDevices();ReconcileVirtualMicrophone();ApplyMixer();InitializeMixerControls();InitializeApplicationDiscovery();InitializeKeyboard();
        if(mixerStore.Recovery!=null)MixerStatus=mixerStore.Recovery;
    }
    private void LoadMixerCards(AudioProfile profile)
    {
        var channelId=SelectedMixerChannel?.Id;var busId=SelectedMixerOutput?.Id;
        mixerLoading=true;
        try
        {
            MixerChannels.Clear();MixerOutputs.Clear();MixerRows.Clear();
            foreach(var c in profile.Channels)MixerChannels.Add(new(c,MixerEdited));
            foreach(var b in profile.Outputs)MixerOutputs.Add(new(b,MixerEdited));
            foreach(var b in MixerOutputs)RefreshOutputChoices(b);
            foreach(var c in profile.Channels)MixerRows.Add(new(c.Name,profile.Outputs.Select(b=>new MixerRouteCell(c.Id,b.Id,c.Name+" → "+b.Name,profile.Routes.Any(r=>r.ChannelId==c.Id&&r.BusId==b.Id&&r.Enabled),MixerEdited)).ToList()));
            foreach(var cell in MixerRows.SelectMany(r=>r.Cells))cell.Blocked=cell.BusId=="virtual"&&profile.Channels.Any(c=>c.Id==cell.ChannelId&&c.NeverSendVirtual);
            MixerProfileName=profile.Name;SelectedMixerChannel=MixerChannels.FirstOrDefault(c=>c.Id==channelId)??MixerChannels.FirstOrDefault();SelectedMixerOutput=MixerOutputs.FirstOrDefault(b=>b.Id==busId)??MixerOutputs.FirstOrDefault();RefreshRecordingSources();
        }
        finally{mixerLoading=false;}
    }
    private void MixerEdited()
    {
        if(mixerLoading||mixerStore==null)return;
        foreach(var cell in MixerRows.SelectMany(r=>r.Cells).Where(r=>r.BusId=="virtual"))
        {var blocked=MixerChannels.Any(c=>c.Id==cell.ChannelId&&c.NeverSendVirtual);cell.Blocked=blocked;}
        bool backendChanged=MixerOutputs.Any(b=>mixerState.Current.Outputs.FirstOrDefault(old=>old.Id==b.Id)?.Backend!=b.Backend);
        mixerState.Current=mixerState.Current with{Channels=MixerChannels.Select(c=>c.Value).ToList(),Outputs=MixerOutputs.Select(b=>b.Value).ToList(),Routes=MixerRows.SelectMany(r=>r.Cells).Select(c=>new AudioRoute(c.ChannelId,c.BusId,c.Enabled)).ToList()};
        if(backendChanged)foreach(var b in MixerOutputs)RefreshOutputChoices(b);
        ReconcileVirtualMicrophone();ApplyMixer();NotifyKeyboard();ScheduleSave();
    }
    private void ApplyMixer()
    {
        try
        {
            MixerStore.Validate(mixerState.Current);
            MixerWarnings=string.Join("\n",RoutingSafety.Inspect(mixerState.Current).Select(x=>T["Runtime6"]+x.Description));
            if(mixerState.Current.Outputs.Any(b=>b.DeviceMode!="Internal"&&b.Volume>1.5))MixerWarnings+="\nNiveau de sortie élevé : baissez le volume avant l’écoute.";
            graph.ApplyMixer(mixerState.Current,enableOutputs);sounds.MasterVolume=1;
            mixerLoading=true;
            try
            {
                var p=mixerState.Current;Muted=p.Channels.Single(c=>c.Id=="mic").Mute;
                Settings.HearMyself=p.Routes.Any(r=>r.ChannelId=="mic"&&r.BusId=="monitor"&&r.Enabled);
                Settings.SendMicrophone=p.Routes.Any(r=>r.ChannelId=="mic"&&r.BusId=="virtual"&&r.Enabled);
                Settings.MonitoringVolume=p.Outputs.Single(b=>b.Id=="monitor").Volume;
                Settings.SoundboardVolume=p.Channels.Single(c=>c.Id=="soundboard").Volume;
                foreach(var name in new[]{nameof(HearMyself),nameof(SendMicrophone),nameof(MonitoringVolume),nameof(MonitoringVolumeSlider),nameof(SoundboardVolume),nameof(SoundboardVolumeSlider)})OnPropertyChanged(name);
            }
            finally{mixerLoading=false;}
        }
        catch(Exception e){MixerStatus=e.Message;}
    }
    private void SyncMixerLegacy(string property)
    {
        if(mixerStore==null||mixerLoading)return;
        switch(property)
        {
            case nameof(HearMyself):foreach(var row in MixerRows)foreach(var cell in row.Cells)if(cell.ChannelId=="mic"&&cell.BusId=="monitor")cell.Enabled=Settings.HearMyself;break;
            case nameof(SendMicrophone):foreach(var row in MixerRows)foreach(var cell in row.Cells)if(cell.ChannelId=="mic"&&cell.BusId=="virtual")cell.Enabled=Settings.SendMicrophone;break;
            case nameof(MonitoringVolume):MixerOutputs.First(b=>b.Id=="monitor").Volume=Settings.MonitoringVolume*100;break;
            case nameof(SoundboardVolume):MixerChannels.First(c=>c.Id=="soundboard").Volume=Settings.SoundboardVolume*100;sounds.MasterVolume=1;break;
            case nameof(Muted):MixerChannels.First(c=>c.Id=="mic").Mute=Muted;break;
        }
    }
    private void RefreshMixerDevices(){RefreshMixerSourceChoices();foreach(var b in MixerOutputs)RefreshOutputChoices(b);_ = RefreshMixerDevicesAsync();}
    [RelayCommand] private async System.Threading.Tasks.Task RefreshMixerDevicesAsync()
    {
        var version=++mixerRefreshVersion;MixerStatus=T["Runtime87"];
        try
        {
            var devices=await System.Threading.Tasks.Task.Run(()=>
            {
                var inputs=mic.GetDevices().ToArray();var outputs=graph.Devices().ToArray();
                var applications=ApplicationDiscoveryService.Shared.Discover(mixerState.Current.Channels).ToArray();
                var mme=Enumerable.Range(0,NAudio.Wave.WaveOut.DeviceCount).Select(i=>new AudioDevice(i.ToString(),NAudio.Wave.WaveOut.GetCapabilities(i).ProductName)).ToArray();
                return (inputs,outputs,applications,mme,asio:MixerDeviceManager.AsioDrivers());
            });
            if(version!=mixerRefreshVersion||lifetime.IsCancellationRequested)return;
            mixerInputCache=devices.inputs;mixerOutputCache=devices.outputs;mixerApplicationCache=devices.applications;mixerMmeCache=devices.mme;mixerAsioCache=devices.asio;
            var previous=mixerLoading;mixerLoading=true;
            try{RefreshMixerSourceChoices();foreach(var output in MixerOutputs)RefreshOutputChoices(output);OnPropertyChanged(nameof(MixerBackends));MixerStatus=T["Runtime85"];}
            finally{mixerLoading=previous;}
        }
        catch(Exception e){MixerStatus=e.Message;}
    }
    private void RefreshOutputChoices(MixerOutputCard output)
    {
        var selected=output.Device;
        var choices=new System.Collections.Generic.List<AudioDevice>{new("@internal","Bus interne · aucune sortie physique")};
        if(output.Backend=="ASIO")foreach(var driver in mixerAsioCache)choices.Add(new(driver,driver));
        else if(output.Backend=="MME")
        {choices.Add(new("-1",T["Runtime100"]));choices.AddRange(mixerMmeCache);}
        else{choices.Add(new("@default",T["Runtime103"]));choices.AddRange(mixerOutputCache);}
        if(!choices.Any(d=>d.Id==selected))choices.Add(new(selected,(output.Value.DeviceName??T["Runtime84"])+" · indisponible"));
        if(output.Choices.SequenceEqual(choices))return;
        output.SetChoices(choices);
    }
    private void SetMixerProfile(AudioProfile profile)
    {
        MixerStore.Validate(profile);
        var primary=profile.Channels.Single(c=>c.Id=="mic");
        if(primary.DeviceId!=null&&primary.DeviceId!=Settings.InputDeviceId)
        {
            refreshingDevices=true;
            try{mic.Stop();graph.FlushMicrophone();Running=false;Settings.InputDeviceId=primary.DeviceId;SelectedDevice=Devices.FirstOrDefault(d=>d.Id==primary.DeviceId);}
            finally{refreshingDevices=false;}
            if(automaticAudio)EnsureMicrophone();
        }
        profile=profile with{Channels=profile.Channels.Select(c=>c.Id=="mic"?c with{DeviceId=Settings.InputDeviceId,DeviceName=SelectedDevice?.Name}:c).ToList()};
        mixerState.Current=profile;LoadMixerCards(profile);RefreshMixerDevices();ReconcileVirtualMicrophone();ApplyMixer();NotifyKeyboard();_=ApplyKeyboardAsync();ScheduleSave();
    }
    [RelayCommand] private void SelectMixerChannel(MixerChannelCard? channel)=>SelectedMixerChannel=channel;
    [RelayCommand] private void SelectMixerOutput(MixerOutputCard? output){SelectedMixerOutput=output;if(output!=null)RefreshOutputChoices(output);}
    [RelayCommand] private void AddMixerOutput()
    {
        if(MixerOutputs.Count>=12)return;
        var bus=new AudioOutputBus{Name="Sortie "+(MixerOutputs.Count+1)};
        SetMixerProfile(mixerState.Current with{Outputs=mixerState.Current.Outputs.Append(bus).ToList(),Routes=mixerState.Current.Routes.Concat(MixerChannels.Select(c=>new AudioRoute(c.Id,bus.Id,false))).ToList()});SelectedMixerOutput=MixerOutputs.Last();
    }
    [RelayCommand] private void FixMixerLoops(){SetMixerProfile(RoutingSafety.Fix(mixerState.Current));MixerStatus=T["Runtime89"];}
    [RelayCommand] private void ResetMixerChannel()
    {if(SelectedMixerChannel is {} c)SetMixerProfile(mixerState.Current with{Channels=mixerState.Current.Channels.Select(x=>x.Id==c.Id?x with{Volume=1,GainDb=0,Pan=0,Mute=false,Solo=false,Effects=new()}:x).ToList()});}
    [RelayCommand] private void SaveAudioProfile()
    {
        if(AudioProfiles.Count>=50){MixerStatus="50 profils maximum.";return;}
        if(string.IsNullOrWhiteSpace(MixerProfileName))return;
        var name=MixerProfileName.Trim();var profile=mixerState.Current with{Id=Guid.NewGuid().ToString("N"),Name=name[..Math.Min(80,name.Length)]};
        mixerState.Profiles.Add(profile);AudioProfiles.Add(profile);SelectedAudioProfile=profile;ScheduleSave();MixerStatus=T["Runtime73"];
    }
    [RelayCommand] private void LoadAudioProfile(){if(SelectedAudioProfile is {} p){SetMixerProfile(p);MixerStatus=T["Runtime68"]+p.Name;}}
    [RelayCommand] private void DuplicateAudioProfile()
    {if(AudioProfiles.Count>=50){MixerStatus="50 profils maximum.";return;}if(SelectedAudioProfile is {} p){var copy=p with{Id=Guid.NewGuid().ToString("N"),Name=p.Name[..Math.Min(70,p.Name.Length)]+" copie"};mixerState.Profiles.Add(copy);AudioProfiles.Add(copy);SelectedAudioProfile=copy;ScheduleSave();}}
    [RelayCommand] private void RenameAudioProfile()
    {if(SelectedAudioProfile is not {} p||string.IsNullOrWhiteSpace(MixerProfileName))return;var name=MixerProfileName.Trim();var next=p with{Name=name[..Math.Min(80,name.Length)]};mixerState.Profiles[mixerState.Profiles.IndexOf(p)]=next;AudioProfiles[AudioProfiles.IndexOf(p)]=next;SelectedAudioProfile=next;ScheduleSave();}
    [RelayCommand] private void UseMixerTemplate(string name)
    {var p=mixerState.Current;SetMixerProfile(p with{Name=name,Routes=p.Routes.Select(r=>r with{Enabled=r.BusId switch{"virtual"=>r.ChannelId is "mic" or "soundboard","monitor"=>r.ChannelId=="soundboard","stream"=>name=="Streaming","recording"=>name=="Recording","master"=>true,_=>false}}).ToList()});}
    private void TickMixer()
    {
        if(mixerStore==null||Page!=8)return;
        foreach(var c in MixerChannels){c.RefreshMeter(graph.Mixer.ChannelMeter(c.Id));c.State=c.Id=="mic"?(Running?"Microphone actif · "+DisplayVoice:T["Runtime56"]):c.Id=="soundboard"?"Lecture locale":c.Id=="keyboard"?KeyboardStatus:graph.DeviceStatuses.FirstOrDefault(s=>s.Id==c.Id)?.Message??"Connexion…";}
        foreach(var b in MixerOutputs){b.RefreshMeter(graph.Mixer.BusMeter(b.Id));b.State=graph.DeviceStatuses.FirstOrDefault(s=>s.Id==b.Id)?.Message??"Connexion…";}
        if(++mixerTicks%10!=0)return;
        var d=graph.Diagnostics;MixerDiagnostics=$"48 000 Hz · float32 · 2 canaux / bus\nBloc : 480 samples / 10 ms · calcul {d.AverageBlockMs:0.00} ms (max {d.MaximumBlockMs:0.00}) · charge {d.AverageBlockMs*10:0.0} %\nBlocs en retard : {d.LateBlocks} · tampons incomplets : {d.Underruns} · samples écartés : {d.DroppedSamples}\nTampon logiciel estimé : n’inclut pas le matériel, la voix IA ou le réseau.";
        var loops=graph.DeviceStatuses.Where(s=>s.Id.StartsWith("loop:")).Select(s=>s.Message);if(loops.Any())MixerWarnings=string.Join("\n",loops);
    }
}
