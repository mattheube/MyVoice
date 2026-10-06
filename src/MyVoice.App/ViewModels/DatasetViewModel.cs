using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.AI;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    public ObservableCollection<DatasetSample> DatasetSamples { get; } = new();
    public ObservableCollection<VoiceRevision> VoiceRevisions { get; } = new();
    [ObservableProperty] private VoiceRevision? selectedRevision;
    [ObservableProperty] private string datasetSummary="";
    [ObservableProperty] private string datasetProgress="";
    private bool voicesWritable=true;
    private void RefreshDataset()
    {
        DatasetSamples.Clear(); VoiceRevisions.Clear();
        if(SelectedAiVoice is not {} voice) { DatasetSummary=""; return; }
        foreach(var sample in voice.Samples)DatasetSamples.Add(sample);
        foreach(var revision in voice.Revisions)VoiceRevisions.Add(revision);
        DatasetSummary=$"{(voice.Samples.Count==0 ? "Référence conservée" : voice.Samples.Count+" échantillons")} · {voice.UsableDuration:0.#} secondes utiles · version {voice.Revision}";
        SelectedRevision=voice.Revisions.FirstOrDefault(r=>r.Number==voice.Revision);
    }
    private async Task AddDatasetFiles(VoiceReference voice, string[] files)
    {
        if(voice.Samples.Count==0 && File.Exists(voice.ReferenceFile))
        {
            var legacy=await Task.Run(()=>DatasetService.Import(store.Root,voice.Id,voice.ReferenceFile,lifetime.Token));
            legacy.Name="Legacy reference";voice.Samples.Add(legacy);SaveVoices();
        }
        for(int i=0;i<files.Length;i++)
        {
            DatasetProgress=$"{i+1}/{files.Length} · {Path.GetFileName(files[i])}";
            var file=files[i];
            var sample=await Task.Run(()=>DatasetService.Import(store.Root,voice.Id,file,lifetime.Token));
            voice.Samples.Add(sample); SaveVoices(); RefreshDataset();
        }
        DatasetProgress=T["DatasetAnalyzed"];
    }
    [RelayCommand] private async Task AddSamplesAsync()
    {
        if(AiBusy||SelectedAiVoice==null)return;
        var dialog=new OpenFileDialog { Filter="Audio|*.wav;*.mp3;*.flac;*.m4a",Multiselect=true };
        if(dialog.ShowDialog()!=true)return;
        AiBusy=true;var voice=SelectedAiVoice;
        try { await AddDatasetFiles(voice,dialog.FileNames); }
        catch(Exception e){StudioError(e.Message);}finally{AiBusy=false;}
    }
    [RelayCommand] private async Task PrepareVoiceAsync()
    {
        if(AiBusy||SelectedAiVoice==null)return;
        StopLive();AiBusy=true;var voice=SelectedAiVoice;
        try { await Task.Run(()=>DatasetService.BuildReference(store.Root,voice)); SaveVoices(); RefreshDataset(); ReloadVoices(); DatasetProgress=T["VoiceCreated"]; }
        catch(Exception e){StudioError(e.Message);}finally{AiBusy=false;}
    }
    [RelayCommand] private void RestoreRevision()
    {
        if(AiBusy||SelectedAiVoice==null||SelectedRevision==null)return;
        if(!File.Exists(SelectedRevision.ReferenceFile)){Status=T["FileMissing"];return;}
        StopLive();SelectedAiVoice.ReferenceFile=SelectedRevision.ReferenceFile;SelectedAiVoice.Revision=SelectedRevision.Number;
        SaveVoices(); RefreshDataset(); ReloadVoices();
    }
}
