using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.AI;
using MyVoice.Audio;
using NAudio.Wave;
namespace MyVoice.App.ViewModels;
public record VoiceComparison(string Mode,string File,double Seconds);
public partial class MainViewModel
{
    public ObservableCollection<VoiceComparison> VoiceComparisons {get;}=new();
    [ObservableProperty] private bool comparisonRecording;
    [ObservableProperty] private string comparisonStatus="";
    private bool finishComparison;
    public bool AiSupportsExpression=>AiModel=="studio"||AiFileModel=="studio";
    [RelayCommand] private void FinishComparison()=>finishComparison=true;
    [RelayCommand] private void PlayComparison(VoiceComparison? result){if(result==null)return;PreviewLocalFile(result.File);}
    [RelayCommand] private async Task CompareFileAsync()
    {
        if(AiBusy||SelectedAiVoice==null)return;
        var dialog=new OpenFileDialog{Filter=MyVoice.Core.MediaValidation.AudioFilter};if(dialog.ShowDialog()!=true)return;
        var normalized=WorkflowFile("comparison.wav");
        try{await Task.Run(()=>MyVoice.Core.AudioCodec.Decode(dialog.FileName,normalized,lifetime.Token));}catch(Exception e){ComparisonStatus=e.Message;return;}
        using(var probe=new AudioFileReader(normalized))if(probe.TotalTime.TotalSeconds>15){ComparisonStatus="Choisissez un extrait de 15 secondes maximum.";return;}
        await RunComparisons(normalized);
    }
    [RelayCommand] private async Task RecordComparisonAsync()
    {
        if(AiBusy||SelectedAiVoice==null||Muted||mic is not MicrophoneService source)return;
        StopLive();EnsureMicrophone();if(!Running)return;
        var samples=new List<float>();var sync=new object();
        void Capture(ReadOnlySpan<float> data){lock(sync){foreach(var value in data){if(samples.Count>=48000*15)break;samples.Add(value);}}}
        finishComparison=false;ComparisonRecording=true;AiBusy=true;source.Samples+=Capture;
        string? path=null;
        try
        {
            var watch=Stopwatch.StartNew();
            while(!finishComparison&&watch.Elapsed.TotalSeconds<15){ComparisonStatus=$"Parlez · {watch.Elapsed.TotalSeconds:0} / 15 s";await Task.Delay(100,lifetime.Token);}
            source.Samples-=Capture;float[] audio;lock(sync)audio=samples.ToArray();
            if(audio.Length<48000||audio.All(x=>Math.Abs(x)<.001)){ComparisonStatus="Enregistrement trop court ou silencieux.";return;}
            var folder=Path.Combine(store.Root,"cache","comparisons",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);path=Path.Combine(folder,"source.wav");
            using var writer=new WaveFileWriter(path,WaveFormat.CreateIeeeFloatWaveFormat(48000,1));writer.WriteSamples(audio,0,audio.Length);
        }
        catch(Exception e){StudioError(e.Message);}
        finally{source.Samples-=Capture;ComparisonRecording=false;AiBusy=false;}
        if(path!=null)await RunComparisons(path);
    }
    private async Task RunComparisons(string input)
    {
        if(SelectedAiVoice==null)return;StopAi();if(liveWorker!=null)await liveWorker;
        AiBusy=true;VoiceComparisons.Clear();var voice=SelectedAiVoice;
        var folder=Path.Combine(store.Root,"cache","comparisons",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            foreach(var mode in new[]{"fast","conversation","studio"})
            {
                var name=mode=="fast"?"Direct":mode=="studio"?"Expressive":"Conversation";ComparisonStatus=name+T["Runtime121"];
                using var engine=new SeedBackend{Model=mode,Profile=AiProfile,Expression=AiExpression,CpuThreads=AiCpuThreads,Similarity=AiSimilarity/100,PreserveDynamics=AiPreserveDynamics,ReduceArtifacts=AiReduceArtifacts,ReferenceFiles=voice.ActiveReferenceFiles};
                await engine.StartAsync(AiPython,AiRepository,Path.Combine(AppContext.BaseDirectory,"voice-engine","worker.py"),AiDevice,lifetime.Token);
                await engine.LoadAsync(voice.ReferenceFile,lifetime.Token);var output=Path.Combine(folder,name+".wav");
                await engine.ConvertFileAsync(input,output,mode=="studio"?30:12,lifetime.Token);VoiceComparisons.Add(new(name,output,engine.LastSeconds));
            }
            ComparisonStatus=T["Runtime108"];
        }
        catch(Exception e){ComparisonStatus=e.Message;}
        finally{AiBusy=false;}
    }
}
