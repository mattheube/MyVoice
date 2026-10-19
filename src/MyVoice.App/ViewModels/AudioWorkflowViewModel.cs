using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.AI;
using MyVoice.Audio;
using MyVoice.Core;
using NAudio.Wave;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    [ObservableProperty] private string conversionInput="";
    [ObservableProperty] private string workflowStatus="";
    [ObservableProperty] private bool conversionRecording;
    [ObservableProperty] private string exportFormat="wav";
    [ObservableProperty] private int exportSampleRate=48000;
    public bool HasAudioResult=>lastConvertedFile!=null&&File.Exists(lastConvertedFile);
    public string[] ExportFormats {get;}=["wav","flac","mp3","ogg","opus"];
    public int[] ExportSampleRates {get;}=[44100,48000];
    private System.Threading.CancellationTokenSource? studioCancellation;
    public bool StudioWorking=>studioCancellation!=null;
    private System.Threading.CancellationToken StudioToken=>studioCancellation?.Token??lifetime.Token;
    private void BeginStudio(){studioCancellation=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);OnPropertyChanged(nameof(StudioWorking));}
    private void EndStudio(){studioCancellation?.Dispose();studioCancellation=null;OnPropertyChanged(nameof(StudioWorking));}
    [RelayCommand] private void CancelStudio(){studioCancellation?.Cancel();graph.StopVoicePlayback();}
    private bool finishConversionRecording;
    [RelayCommand] private void FinishConversionRecording()=>finishConversionRecording=true;
    [RelayCommand] private async Task ChooseConversionAudioAsync(){var picker=new OpenFileDialog{Filter=MediaValidation.AudioFilter};if(picker.ShowDialog()==true)await SetConversionInput(picker.FileName);}
    public async Task SetConversionInput(string file)
    {
        if(AiBusy)return;AiBusy=true;BeginStudio();
        try{var path=WorkflowFile("input.wav");await Task.Run(()=>AudioCodec.Decode(file,path,StudioToken));ConversionInput=path;WorkflowStatus=Path.GetFileName(file);}
        catch(OperationCanceledException){WorkflowStatus=T["WorkflowCancelled"];}catch(Exception e){WorkflowStatus=e.Message;}finally{EndStudio();AiBusy=false;}
    }
    private string WorkflowFile(string name){var folder=Path.Combine(store.Root,"cache","studio",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);return Path.Combine(folder,name);}
    [RelayCommand] private async Task RecordConversionAudioAsync()
    {
        if(AiBusy||Muted||mic is not MicrophoneService source)return;
        var started=!Running;StopLive();EnsureMicrophone();if(!Running)return;
        var samples=new List<float>();var gate=new object();
        void Capture(ReadOnlySpan<float> audio){lock(gate){foreach(var sample in audio){if(samples.Count>=48000*120)break;samples.Add(sample);}}}
        AiBusy=true;BeginStudio();ConversionRecording=true;finishConversionRecording=false;source.Samples+=Capture;
        try{var begin=DateTime.UtcNow;while(!finishConversionRecording&&(DateTime.UtcNow-begin).TotalSeconds<120){WorkflowStatus=$"{T["Recording"]} · {(DateTime.UtcNow-begin).TotalSeconds:0} / 120 s";await Task.Delay(100,StudioToken);}source.Samples-=Capture;float[] audio;lock(gate)audio=samples.ToArray();if(audio.Length<48000)throw new InvalidDataException(T["ReferenceTooShort"]);var path=WorkflowFile("recording.wav");using(var writer=new WaveFileWriter(path,WaveFormat.CreateIeeeFloatWaveFormat(48000,1)))writer.WriteSamples(audio,0,audio.Length);ConversionInput=path;WorkflowStatus=T["AudioReadyToConvert"];}
        catch(OperationCanceledException){WorkflowStatus=T["WorkflowCancelled"];}catch(Exception e){WorkflowStatus=e.Message;}finally{source.Samples-=Capture;ConversionRecording=false;EndStudio();AiBusy=false;if(started&&Running)ToggleCapture();}
    }
    [RelayCommand] private async Task ConvertSelectedAudioAsync()
    {
        if(AiBusy||SelectedAiVoice==null||!File.Exists(ConversionInput))return;
        var voice=SelectedAiVoice;StopAi();if(liveWorker!=null)await liveWorker;AiBusy=true;BeginStudio();WorkflowStatus=T["Converting"];ConversionProgress=0;
        try{using var engine=new SeedBackend{Model=AiFileModel,Profile=AiProfile,Expression=AiExpression,CpuThreads=AiCpuThreads,Similarity=AiSimilarity/100,PreserveDynamics=AiPreserveDynamics,ReduceArtifacts=AiReduceArtifacts,ReferenceFiles=voice.ActiveReferenceFiles};engine.Progress+=p=>System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(()=>ConversionProgress=p*100));await engine.StartAsync(AiPython,AiRepository,Path.Combine(AppContext.BaseDirectory,"voice-engine","worker.py"),AiDevice,StudioToken);await engine.LoadAsync(voice.ReferenceFile,StudioToken);var output=WorkflowFile("converted.wav");await engine.ConvertFileAsync(ConversionInput,output,AiProfile=="Quality"?40:AiProfile=="Low latency"?8:24,StudioToken);lastConvertedFile=output;OnPropertyChanged(nameof(HasAudioResult));WorkflowStatus=T["AudioResultReady"];ConversionProgress=100;}
        catch(OperationCanceledException){WorkflowStatus=T["WorkflowCancelled"];}catch(Exception e){WorkflowStatus=e.Message;}finally{EndStudio();AiBusy=false;}
    }
    [RelayCommand] private async Task ExportAudioResultAsync()
    {
        if(AiBusy||lastConvertedFile==null||!File.Exists(lastConvertedFile))return;
        var dialog=new SaveFileDialog{Filter=$"{ExportFormat.ToUpperInvariant()}|*.{ExportFormat}",FileName="MyVoice-result."+ExportFormat};if(dialog.ShowDialog()!=true)return;
        AiBusy=true;BeginStudio();try{var temporary=Path.Combine(Path.GetDirectoryName(dialog.FileName)!,".myvoice-"+Guid.NewGuid()+"."+ExportFormat);try{await Task.Run(()=>AudioCodec.Export(lastConvertedFile,temporary,ExportFormat,ExportSampleRate,StudioToken));File.Move(temporary,dialog.FileName,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}WorkflowStatus=dialog.FileName;}catch(OperationCanceledException){WorkflowStatus=T["WorkflowCancelled"];}catch(Exception e){WorkflowStatus=e.Message;}finally{EndStudio();AiBusy=false;}
    }
    [RelayCommand] private void PreviewAudioResult(){if(lastConvertedFile!=null)PreviewLocalFile(lastConvertedFile);}
    [RelayCommand] private async Task PlayAudioResultToVoiceAsync()
    {
        if(AiBusy||lastConvertedFile==null||!File.Exists(lastConvertedFile))return;
        if(string.IsNullOrEmpty(graph.VirtualId)){WorkflowStatus=T["ChooseOutput"];return;}
        AiBusy=true;BeginStudio();
        try{var normalized=WorkflowFile("play.wav");await Task.Run(()=>AudioCodec.Decode(lastConvertedFile,normalized,StudioToken));using var reader=new AudioFileReader(normalized);var block=new float[4800];int count;while((count=reader.Read(block,0,block.Length))>0){while(graph.VoicePlaybackFrames>48000)await Task.Delay(30,StudioToken);graph.PushVoicePlayback(block.AsSpan(0,count));}while(graph.VoicePlaybackFrames>0)await Task.Delay(30,StudioToken);WorkflowStatus=T["AudioPlaybackDone"];}
        catch(OperationCanceledException){WorkflowStatus=T["WorkflowCancelled"];}catch(Exception e){WorkflowStatus=e.Message;}finally{graph.StopVoicePlayback();EndStudio();AiBusy=false;}
    }
}
