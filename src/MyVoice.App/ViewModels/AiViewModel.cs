using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.AI;
using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.App.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private readonly SeedBackend ai = new();
    private readonly SampleRing aiInput = new(48000*30);
    private CancellationTokenSource? liveCancel;
    private Task? liveWorker;
    private int aiGeneration;
    public ObservableCollection<VoiceReference> AiVoices { get; } = new();
    [ObservableProperty] private VoiceReference? selectedAiVoice;
    [ObservableProperty] private string aiState = "Offline";
    [ObservableProperty] private bool aiBusy;
    [ObservableProperty] private bool aiReady;
    [ObservableProperty] private bool aiLive;
    [ObservableProperty] private string aiMetrics = "";
    [ObservableProperty] private string aiDetails = "";
    [ObservableProperty] private string aiLog = "";
    public string AiPython
    {
        get => Settings.AiPython; set
        {
            Settings.AiPython = value;
            Changed(nameof(AiPython));
        }
    }
    public string AiRepository
    {
        get => Settings.AiRepository; set
        {
            Settings.AiRepository = value;
            Changed(nameof(AiRepository));
        }
    }
    public string AiDevice
    {
        get => Settings.AiDevice; set
        {
            if(Settings.AiDevice==value)return;
            Settings.AiDevice = value;
            if(AiReady) { StopAi(); aiManualStop=false; _=StartAiCommand.ExecuteAsync(null); }
            Changed(nameof(AiDevice));
        }
    }
    public string AiProfile
    {
        get => Settings.AiProfile; set
        {
            Settings.AiProfile = value;ai.Profile=value;
            Changed(nameof(AiProfile));
        }
    }
    public object[] AiDeviceChoices { get; } = [new { Id="auto", Name="Automatique (recommandé)" }, new { Id="cuda", Name="Carte graphique NVIDIA (CUDA)" }, new { Id="cpu", Name="Processeur (CPU, plus lent)" }];
    public object[] AiProfileChoices { get; } = [new { Id="Low latency", Name="Réponse rapide" }, new { Id="Balanced", Name="Équilibré" }, new { Id="Quality", Name="Qualité maximale" }];
    private double aiRtf, aiLatency;
    private void InitializeAi()
    {
        var file = Path.Combine(store.Root, "voices", "voices.json");
        try
        {
            if (File.Exists(file))
            foreach (var v in JsonSerializer.Deserialize<List<VoiceReference>>(File.ReadAllText(file)) ?? [])
                { v.Samples ??=new();v.Revisions ??=new();AiVoices.Add(v); }
        }
        catch (Exception e) { voicesWritable=false; store.Log(e.Message); }
        SelectedAiVoice = AiVoices.FirstOrDefault(v => v.Id == Settings.SelectedAiVoice) ?? AiVoices.FirstOrDefault();
        if(Settings.ActiveAiVoice && Settings.LastAiVoiceId==null && SelectedAiVoice!=null){Settings.LastVoiceIsAi=true;Settings.LastAiVoiceId=SelectedAiVoice.Id;}
        var runtime = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyVoice", "AI", "runtime.json");
        if (File.Exists(runtime) && !File.Exists(Settings.AiPython))
        {
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(runtime));
                Settings.AiPython = json.RootElement.GetProperty("Python").GetString()!;
                Settings.AiRepository = json.RootElement.GetProperty("Repository").GetString()!;
            }
            catch (Exception e) { store.Log(e.Message); }
        }
        ai.Log += line => { store.Log("AI: " + line); Application.Current.Dispatcher.BeginInvoke(new Action(() => AiLog = (AiLog + line + Environment.NewLine)[Math.Max(0, (AiLog + line + Environment.NewLine).Length - 8000)..])); };
        ai.Similarity=Settings.AiSimilarity;ai.PreserveDynamics=Settings.AiPreserveDynamics;ai.ReduceArtifacts=Settings.AiReduceArtifacts;ai.CpuThreads=Settings.AiCpuThreads;ai.Model=Settings.AiModel;ai.Expression=Settings.AiExpression;ai.Profile=Settings.AiProfile;
        graph.AiVirtualSubmitted+=ms=>conversationLatency=ms;
        ai.Progress+=value=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>ConversionProgress=value*100));
        graph.AiInput += FeedAi;
    }
    private void FeedAi(ReadOnlySpan<float> samples)
    {
        if(!AiLive||!aiAcceptInput)return;
        if(AiModel=="conversation"){conversationInput.Write(samples,graph.AiInputTimestamp);return;}
        if(PhraseMode)
        {
            double energy=0;foreach(var value in samples)energy+=value*value;
            if(Math.Sqrt(energy/Math.Max(1,samples.Length))>.003){aiSpeechSeen=true;Interlocked.Exchange(ref aiQuietSamples,0);}
            else Interlocked.Add(ref aiQuietSamples,samples.Length);
            if(!aiSpeechSeen)return;
        }
        aiInput.Write(samples);
    }
    private bool CanStartAi() => !AiBusy && !AiReady;
    private bool CanUseAi() => AiReady && !AiBusy && SelectedAiVoice != null;
    partial void OnAiBusyChanged(bool value)
    {
        StartAiCommand.NotifyCanExecuteChanged();
        ConvertFileCommand.NotifyCanExecuteChanged();
    }
    partial void OnAiReadyChanged(bool value)
    {
        StartAiCommand.NotifyCanExecuteChanged();
        ConvertFileCommand.NotifyCanExecuteChanged();
    }
    private void SaveVoices()
    {
        if(!voicesWritable) throw new InvalidDataException("Voice catalog could not be read; original file preserved.");
        var path = Path.Combine(store.Root, "voices", "voices.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(AiVoices));
        if(File.Exists(path)) File.Copy(path,path+".backup",true);
        File.Move(path + ".tmp", path, true);
    }
    partial void OnSelectedAiVoiceChanged(VoiceReference? value)
    {
        if(value!=null) Settings.SelectedAiVoice = value.Id;
        RefreshDataset();
        OnPropertyChanged(nameof(DisplayVoiceCover));OnPropertyChanged(nameof(DisplayVoice));
        ConvertFileCommand.NotifyCanExecuteChanged();
        AiDetails = value == null ? "" : $"{value.Duration:0.0}s · Peak {value.PeakDb:0.0} dB · RMS {value.RmsDb:0.0} dB";
        if (AiLive)
            StopLive();
        ScheduleSave();
    }
    [RelayCommand]
    private async Task ImportVoiceAsync()
    {
        if(AiBusy || !voicesWritable) return;
        var name=Dialogs.Prompt(T["VoiceName"]);if(name==null)return;
        var dialog=new OpenFileDialog { Filter="Audio|*.wav;*.mp3;*.flac;*.m4a",Multiselect=true };
        if(dialog.ShowDialog()!=true)return;
        var voice=new VoiceReference { Name=name };
        AiVoices.Add(voice);SelectedAiVoice=voice;AiBusy=true;
        try
        {
            await AddDatasetFiles(voice,dialog.FileNames);
            await Task.Run(()=>DatasetService.BuildReference(store.Root,voice));
            SaveVoices(); RefreshDataset(); ReloadVoices(); Status=T["VoiceCreated"];
        }
        catch(Exception e){SaveVoices();StudioError(e.Message);}
        finally{AiBusy=false;}
    }
    [RelayCommand]
    private void RenameAiVoice()
    {
        if (SelectedAiVoice == null)
            return;
        var name = Dialogs.Prompt(T["Rename"], SelectedAiVoice.Name);
        if (name == null)
            return;
        SelectedAiVoice.Name = name;
        ReloadVoices();
        SaveVoices();
    }
    [RelayCommand]
    private void DeleteAiVoice()
    {
        if (SelectedAiVoice == null || !Dialogs.Confirm(T["DeleteVoiceConfirm"]))
            return;
        StopAi();
        AiVoices.Remove(SelectedAiVoice);
        SelectedAiVoice = AiVoices.FirstOrDefault();
        SaveVoices();
    }
    [RelayCommand]
    private void CoverAiVoice()
    {
        if (SelectedAiVoice == null)
            return;
        try
        {
            var cover = ImportCover(true, SelectedAiVoice.Cover);
            if (cover == null) return;
            SelectedAiVoice.Cover = cover;
            ReloadVoices();
            SaveVoices();
        }
        catch (Exception e) { Error(e); }
    }
    private void ReloadVoices()
    {
        var selected = SelectedAiVoice;
        var list = AiVoices.ToArray();
        AiVoices.Clear();
        foreach (var v in list)
            { v.Samples ??=new();v.Revisions ??=new();AiVoices.Add(v); }
        SelectedAiVoice = selected;
    }
    [RelayCommand]
    private void BrowsePython()
    {
        var dialog = new OpenFileDialog { Filter = "Python|python.exe" };
        if (dialog.ShowDialog() == true)
            AiPython = dialog.FileName;
    }
    [RelayCommand]
    private void BrowseAiRepository()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
            AiRepository = dialog.FolderName;
    }
    [RelayCommand(CanExecute = nameof(CanStartAi))]
    private async Task StartAiAsync()
    {
        if (AiBusy) return;
        aiManualStop=false;
        AiBusy = true;
        AiState = T["AiLoading"];
        try
        {
            await ai.StartAsync(AiPython, AiRepository, Path.Combine(AppContext.BaseDirectory, "voice-engine", "worker.py"), AiDevice, lifetime.Token);
            AiReady = ai.Ready;
            AiState = "Ready · " + ai.Device;
            Save();
        }
        catch (Exception e) { AiReady = false; AiState = "Moteur indisponible · " + e.Message; StudioError(e.Message); }
        finally { AiBusy = false; }
        if(AiReady && Settings.ActiveAiVoice && VoiceEnabled && !aiActivating && !lifetime.IsCancellationRequested) await UseAiVoiceAsync(SelectedAiVoice);
    }
    [RelayCommand]
    public void StopAi()
    {
        aiManualStop=true;
        if (installerProcess != null && !installerProcess.HasExited)
            installerProcess.Kill(true);
        StopLive();
        ai.Stop();
        AiReady = false;
        AiBusy = false;
        AiState = "Offline";
    }
    [RelayCommand(CanExecute = nameof(CanUseAi))]
    private async Task ConvertFileAsync()
    {
        if (!AiReady || SelectedAiVoice == null || AiBusy)
        {
            Status = T["AiSelectFirst"];
            return;
        }
        var input = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3;*.flac" };
        if (input.ShowDialog() != true)
            return;
        var output = new SaveFileDialog { Filter = "WAV|*.wav", FileName = "MyVoice-converted.wav" };
        if (output.ShowDialog() != true)
            return;
        StopLive();
        AiBusy = true;
        AiState = T["Converting"];
        ConversionProgress=0;
        try
        {
            if (liveWorker != null) await liveWorker;
            if(ai.Model!=AiFileModel){ai.Stop();ai.Model=AiFileModel;}
            if (!ai.Ready) await ai.StartAsync(AiPython, AiRepository, Path.Combine(AppContext.BaseDirectory, "voice-engine", "worker.py"), AiDevice, lifetime.Token);
            AiReady = ai.Ready;
            ai.ReferenceFiles=SelectedAiVoice.Samples.Where(s=>!s.Excluded).SelectMany(s=>s.Segments.OrderByDescending(x=>x.Score).Take(3)).Select(s=>s.File).Where(File.Exists).ToArray();
            await ai.LoadAsync(SelectedAiVoice.ReferenceFile, lifetime.Token);
            await ai.ConvertFileAsync(input.FileName, output.FileName, AiFileModel=="studio"?(AiProfile=="Quality"?40:AiProfile=="Low latency"?20:30):AiProfile=="Quality"?24:AiProfile=="Low latency"?6:12, lifetime.Token);
            AiState = "Ready · " + ai.Device;
            lastConvertedFile=output.FileName;
            Status = T["Converted"] + output.FileName;
        }
        catch (Exception e) { AiReady = ai.Ready; StudioError(e.Message); AiState = "Error"; }
        finally { ai.Stop();ai.Model=AiModel;AiReady=false;AiBusy = false; }
    }
    [RelayCommand]
    private async Task ToggleAiLiveAsync()
    {
        if (AiLive)
        {
            StopLive();
            return;
        }
        if (!AiReady || SelectedAiVoice == null || AiBusy || !Running)
        {
            Status = T["AiLiveRequirements"];
            return;
        }
        if(ai.Device=="CPU") Status="CPU : conversion par phrases, qualité conservée. Le calcul peut dépasser dix secondes.";
        AiBusy = true;
        try
        {
            if(liveWorker!=null)await liveWorker;
            ai.ReferenceFiles=SelectedAiVoice.Samples.Where(s=>!s.Excluded).SelectMany(s=>s.Segments.OrderByDescending(x=>x.Score).Take(3)).Select(s=>s.File).Where(File.Exists).ToArray();
            await ai.LoadAsync(SelectedAiVoice.ReferenceFile, lifetime.Token);
            OnPropertyChanged(nameof(AiAdvancedMetrics));
            aiInput.Clear();conversationInput.Clear();
            graph.FlushMicrophone();
            liveCancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            AiLive = true;
            graph.AiLive = true;
            var liveToken = liveCancel.Token;
            liveWorker = Task.Run(() => LiveLoop(liveToken));
        }
        catch (Exception e) { StudioError(e.Message); }
        finally { AiBusy = false; }
    }
    private volatile bool aiAcceptInput=true;
    private async Task LiveLoop(CancellationToken token)
    {
        if(AiModel=="conversation"){await ConversationLoop(token);return;}
        bool phrases=PhraseMode;
        int count=phrases?48000*8:AiProfile=="Low latency"?24000:48000;

        aiAcceptInput=true;aiSpeechSeen=false;finishAiPhrase=false;aiQuietSamples=0;
        try
        {
            while(!token.IsCancellationRequested)
            {
                await Application.Current.Dispatcher.InvokeAsync(()=>AiState=phrases?"À vous de parler · pause pour terminer (8 s maximum)":"Écoute en cours");
                while(aiInput.Count<count && !(phrases && aiInput.Count>=96000 && (Volatile.Read(ref aiQuietSamples)>=33600||finishAiPhrase)))await Task.Delay(20,token);
                if(phrases)aiAcceptInput=false;
                var generation=Volatile.Read(ref aiGeneration);var input=new float[Math.Min(count,aiInput.Count)];aiInput.Take(input);
                await Application.Current.Dispatcher.InvokeAsync(()=>AiState="Conversion fidèle en cours…");
                var watch=System.Diagnostics.Stopwatch.StartNew();
        int steps=AiModel=="studio"?(AiProfile=="Quality"?40:AiProfile=="Low latency"?20:30):AiProfile=="Quality"?24:AiProfile=="Low latency"?6:12;
                var output=await ai.ConvertChunkAsync(input,steps,token);
                token.ThrowIfCancellationRequested();
                if(Muted||generation!=Volatile.Read(ref aiGeneration))Array.Clear(output);
                graph.PushConverted(output);
                aiRtf=watch.Elapsed.TotalSeconds/(input.Length/48000d);
                aiLatency=watch.Elapsed.TotalMilliseconds+input.Length/48d;
                if(phrases)
                {
                    await Application.Current.Dispatcher.InvokeAsync(()=>AiState="Lecture de la phrase convertie · microphone en attente");
                    while(graph.ConvertedFrames>0)await Task.Delay(30,token);
                    aiInput.Clear();aiSpeechSeen=false;finishAiPhrase=false;aiQuietSamples=0;aiAcceptInput=true;
                }
            }
        }
        catch(OperationCanceledException) { }
        catch(Exception e){StudioError(e.Message);_=Application.Current.Dispatcher.BeginInvoke(new Action(()=>{StopLive();ai.Stop();AiReady=false;AiState=e.Message;nextAiRetry=DateTime.UtcNow.AddSeconds(10);}));}
        finally{aiAcceptInput=true;}
    }
    private void StopLive()
    {
        graph.AiLive = false;
        AiLive = false;
        liveCancel?.Cancel();
        liveCancel = null;
        aiInput.Clear();conversationInput.Clear();
        graph.FlushMicrophone();
    }
    private void TickAi()
    {
        AiMetrics = aiLatency <= 0 ? "—" : $"~{aiLatency:0} ms · RTF {aiRtf:0.00} · calcul / audio {aiRtf:0.00}×";
        if (AiReady && !ai.Ready)
        {
            AiReady = false;
            AiState = "Offline";
            StopLive();
        }
        OnPropertyChanged(nameof(DisplayVoice));OnPropertyChanged(nameof(DisplayVoiceCover));
        OnPropertyChanged(nameof(ConversationMetrics));
    }
    private void DisposeAi()
    {
        if (installerProcess != null && !installerProcess.HasExited)
            installerProcess.Kill(true);
        StopLive();
        graph.AiInput -= FeedAi;
        ai.Dispose();
    }
}
