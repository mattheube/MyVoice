using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private bool aiManualStop, aiActivating;
    private readonly System.Threading.SemaphoreSlim voiceSwitchGate=new(1,1);
    private int voiceSwitchRevision;
    private string? activeAiVoiceId;
    private VoiceReference? ActiveAiVoice=>AiVoices.FirstOrDefault(v=>v.Id==activeAiVoiceId)??(communityTrialVoice?.Id==activeAiVoiceId?communityTrialVoice:null);
    private VoiceReference? ResumeAiVoice=>ActiveAiVoice??AiVoices.FirstOrDefault(v=>v.Id==Settings.LastAiVoiceId)??SelectedAiVoice;
    private int aiRestarts;
    private DateTime nextAiRetry;
    [ObservableProperty] private double conversionProgress;
    public double AiSimilarity { get=>Settings.AiSimilarity*100; set { Settings.AiSimilarity=Math.Clamp(value/100,0,1);ai.Similarity=Settings.AiSimilarity;Changed(nameof(AiSimilarity)); } }
    public bool AiPreserveDynamics { get=>Settings.AiPreserveDynamics;set{Settings.AiPreserveDynamics=value;ai.PreserveDynamics=value;Changed(nameof(AiPreserveDynamics));} }
    public bool AiReduceArtifacts { get=>Settings.AiReduceArtifacts;set{Settings.AiReduceArtifacts=value;ai.ReduceArtifacts=value;Changed(nameof(AiReduceArtifacts));} }
    public int AiCpuThreads { get=>Settings.AiCpuThreads;set{Settings.AiCpuThreads=Math.Clamp(value,1,Math.Max(1,Environment.ProcessorCount-1));ai.CpuThreads=Settings.AiCpuThreads;Changed(nameof(AiCpuThreads));} }
    public string AiAdvancedMetrics => $"{(AiModel=="studio" ? "Seed-VC V2 · phrases" : AiModel=="conversation"?"Seed-VC V2 · continu":"Seed-VC classique")} · VRAM {ai.VramMb:0} Mo · références {ai.ReferenceCount}";
    public string? DisplayVoiceCover => VoiceEnabled && AiLive ? ActiveAiVoice?.Cover : null;
    public string LastVoiceName => Product.Presets.FirstOrDefault(p=>p.Id==Settings.LastDesignedVoiceId)?.Name ?? (Settings.LastVoiceIsAi ? AiVoices.FirstOrDefault(v=>v.Id==Settings.LastAiVoiceId)?.Name??Settings.LastModifiedVoice : Settings.LastModifiedVoice);
    public string VoiceSwitchLabel => "Default Clean ↔ " + LastVoiceName;
    public string DisplayVoice => !VoiceEnabled ? "Default Clean" : AiLive ? ActiveAiVoice?.Name ?? "Default Clean" : graph.Processor.Designed is {} designed?designed.Name:CurrentPreset=="Clean"?"Default Clean":CurrentPreset;
    [RelayCommand(AllowConcurrentExecutions=true)] private async Task UseAiVoiceAsync(VoiceReference? voice)
    {
        if(voice==null||!voice.Ready){Status=T["FileMissing"];return;}
        if(AiBusy&&!aiActivating){Status=T["Runtime112"];return;}
        var request=++voiceSwitchRevision;
        try{await voiceSwitchGate.WaitAsync(lifetime.Token);}catch(OperationCanceledException){return;}
        try
        {
            if(request!=voiceSwitchRevision)return;
            aiActivating=true;aiManualStop=false;StopLive();if(liveWorker!=null)await liveWorker;
            if(request!=voiceSwitchRevision)return;
            SelectedAiVoice=voice;graph.Processor.Designed=null;graph.Processor.Preset="Clean";Settings.VoicePreset="Clean";Settings.ActiveDesignedVoiceId=null;Settings.ActiveAiVoice=false;
            OnPropertyChanged(nameof(DisplayVoice));OnPropertyChanged(nameof(DisplayVoiceCover));
            EnsureMicrophone();if(!ai.Ready)await StartAiAsync();
            if(request!=voiceSwitchRevision)return;
            if(!AiReady||!Running)throw new InvalidOperationException(T["Runtime49"]);
            await ToggleAiLiveAsync();
            if(request!=voiceSwitchRevision)return;
            if(!AiLive)throw new InvalidOperationException(T["Runtime50"]);
        }
        catch(OperationCanceledException){}
        catch(Exception e){if(request==voiceSwitchRevision){SelectPreset(Presets.First(p=>p.Name=="Clean"));Status=e.Message;}}
        finally{aiActivating=false;voiceSwitchGate.Release();}
    }
    private void ConfirmActiveAiVoice(VoiceReference voice)
    {
        activeAiVoiceId=voice.Id;foreach(var preset in Presets)preset.Selected=false;
        Settings.ActiveAiVoice=true;Settings.VoiceEnabled=true;graph.Processor.Enabled=true;
        if(voice!=communityTrialVoice){Settings.LastDesignedVoiceId=null;Settings.LastVoiceIsAi=true;Settings.LastAiVoiceId=voice.Id;}
        OnPropertyChanged(nameof(VoiceEnabled));OnPropertyChanged(nameof(DisplayVoice));OnPropertyChanged(nameof(DisplayVoiceCover));OnPropertyChanged(nameof(VoiceSwitchLabel));ScheduleSave();
    }
    [RelayCommand] private void ToggleVoice() => VoiceEnabled=!VoiceEnabled;
    [RelayCommand] private void ToggleMonitoring() => HearMyself=!HearMyself;
    [RelayCommand] private void QuickSwitchVoice()
    {
        if(VoiceEnabled && (Settings.ActiveAiVoice || graph.Processor.Designed!=null || CurrentPreset!="Clean"))VoiceEnabled=false;
        else if(Product.Presets.FirstOrDefault(p=>p.Id==Settings.LastDesignedVoiceId) is {} designed)UseDesignedVoice(designed);
        else if(Settings.LastVoiceIsAi && AiVoices.FirstOrDefault(v=>v.Id==Settings.LastAiVoiceId) is {} voice)_=UseAiVoiceCommand.ExecuteAsync(voice);
        else SelectPreset(Presets.FirstOrDefault(p=>p.Name==Settings.LastModifiedVoice)??Presets[1]);
    }
    [RelayCommand] private void OpenLastSound()
    {
        var sound=SoundCards.FirstOrDefault(s=>s.Item.Id==Settings.LastSoundId);
        if(sound!=null){SelectedSound=sound;NowPlayingVisible=true;}
    }
    [RelayCommand] private void SelectSound(SoundCard? sound) { if(sound!=null)SelectedSound=sound; }
    public string LastSoundName => SoundCards.FirstOrDefault(s=>s.Item.Id==Settings.LastSoundId)?.Name ?? "—";
    public string? LastSoundCover => SoundCards.FirstOrDefault(s=>s.Item.Id==Settings.LastSoundId)?.Cover;
}
