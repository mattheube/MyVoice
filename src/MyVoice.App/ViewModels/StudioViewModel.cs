using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.Soundboard;
using MyVoice.App.Services;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private SoundboardService sounds = null!;
    private AudioGraph graph = null!;
    public ObservableCollection<AudioDevice> OutputDevices { get; } = new();
    public ObservableCollection<SoundCard> SoundCards { get; } = new();
    public ObservableCollection<SoundFolder> Folders { get; } = new();
    public System.ComponentModel.ICollectionView SoundView { get; private set; } = null!;
    public ObservableCollection<PresetCard> Presets { get; } = new(new[] { new PresetCard("Clean", "ESSENTIAL", "00", "Natural signal"), new("Deep", "CHARACTER", "01", "Low & grounded"), new("High", "CHARACTER", "02", "Bright register"), new("Robot", "SYNTHETIC", "03", "Ring modulation"), new("Radio", "TEXTURE", "04", "On the air"), new("Walkie Talkie", "TEXTURE", "05", "Narrow transmission"), new("Megaphone", "TEXTURE", "06", "Cut through"), new("Echo", "SPACE", "07", "A little distance"), new("Demon", "CHARACTER", "08", "Dark resonance"), new("Tiny", "CHARACTER", "09", "Small & spirited") });
    [ObservableProperty] private AudioDevice? monitoringDevice;
    [ObservableProperty] private AudioDevice? virtualDevice;
    [ObservableProperty] private SoundFolder? selectedFolder;
    [ObservableProperty] private SoundCard? selectedSound;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private double soundPosition;
    [ObservableProperty] private double soundDuration = 1;
    [ObservableProperty] private string soundTime = "00:00 / 00:00";
    [ObservableProperty] private string soundState = "";
    [ObservableProperty] private bool nowPlayingVisible;
    [ObservableProperty] private string routeStatus = "";
    [ObservableProperty] private double outputMeter;
    private bool loadingStudio = true, updatingPosition;
    public bool HearMyself
    {
        get => Settings.HearMyself; set
        {
            Settings.HearMyself = value;
            graph.HearMyself = value;
            Changed(nameof(HearMyself));
        }
    }
    public bool SendMicrophone
    {
        get => Settings.SendMicrophone; set
        {
            Settings.SendMicrophone = value;
            graph.SendMicrophone = value;
            Changed(nameof(SendMicrophone));
        }
    }
    public double MonitoringVolume
    {
        get => Settings.MonitoringVolume * 100; set
        {
            Settings.MonitoringVolume = double.IsFinite(value) ? Math.Max(0,value / 100) : .5;
            graph.MonitorVolume = (float)Math.Min(1e6,Settings.MonitoringVolume);
            Changed(nameof(MonitoringVolume));OnPropertyChanged(nameof(MonitoringVolumeSlider));
        }
    }
    public double SoundboardVolume
    {
        get => Settings.SoundboardVolume * 100; set
        {
            Settings.SoundboardVolume = double.IsFinite(value) ? Math.Max(0,value / 100) : 1;
            sounds.MasterVolume = (float)Math.Min(1e6,Settings.SoundboardVolume);
            Changed(nameof(SoundboardVolume));OnPropertyChanged(nameof(SoundboardVolumeSlider));
        }
    }
    public bool AllowMultipleSounds
    {
        get => Settings.AllowMultipleSounds; set
        {
            Settings.AllowMultipleSounds = value;
            sounds.AllowMultiple = value;
            Changed(nameof(AllowMultipleSounds));
        }
    }
    public string CurrentPreset => Settings.VoicePreset;
    public string CurrentPresetDisplay => CurrentPreset=="Clean"?"Default Clean":CurrentPreset;
    public bool VoiceEnabled
    {
        get => Settings.VoiceEnabled; set
        {
            Settings.VoiceEnabled = value;
            graph.Processor.Enabled = value;
            if(!value) StopLive(); else if(Settings.ActiveAiVoice&&!AiLive) _=UseAiVoiceCommand.ExecuteAsync(SelectedAiVoice);
            OnPropertyChanged(nameof(DisplayVoice));OnPropertyChanged(nameof(DisplayVoiceCover));
            Changed(nameof(VoiceEnabled));
        }
    }
    public double VoiceIntensity
    {
        get => Settings.VoiceIntensity * 100; set
        {
            Settings.VoiceIntensity = Math.Clamp(value / 100, 0, 1);
            graph.Processor.Intensity = Settings.VoiceIntensity;
            Changed(nameof(VoiceIntensity));
        }
    }
    public System.ComponentModel.ICollectionView PresetView { get; private set; } = null!;
    [ObservableProperty] private string voiceSearch = "";
    [ObservableProperty] private bool favoritesOnly;
    partial void OnVoiceSearchChanged(string value) => PresetView?.Refresh();
    partial void OnFavoritesOnlyChanged(bool value) => PresetView?.Refresh();
    public bool FavoritePreset
    {
        get => Settings.FavoriteVoices.Contains(CurrentPreset); set
        {
            if (value && !FavoritePreset)
                Settings.FavoriteVoices.Add(CurrentPreset);
            if (!value)
                Settings.FavoriteVoices.Remove(CurrentPreset);
            Changed(nameof(FavoritePreset));
            PresetView.Refresh();
        }
    }
    public bool SoundHear
    {
        get => SelectedSound?.Item.HearMyself ?? true; set
        {
            if (SelectedSound == null)
                return;
            SelectedSound.Item.HearMyself = value;
            SoundChanged(nameof(SoundHear));
        }
    }
    public bool SoundSend
    {
        get => SelectedSound?.Item.SendToMic ?? true; set
        {
            if (SelectedSound == null)
                return;
            SelectedSound.Item.SendToMic = value;
            SoundChanged(nameof(SoundSend));
        }
    }
    public bool SoundLoop
    {
        get => SelectedSound?.Item.Loop ?? false; set
        {
            if (SelectedSound == null)
                return;
            SelectedSound.Item.Loop = value;
            SoundChanged(nameof(SoundLoop));
        }
    }
    public double SoundVolume
    {
        get => (SelectedSound?.Item.Volume ?? 1) * 100; set
        {
            if (SelectedSound == null)
                return;
            SelectedSound.Item.Volume = double.IsFinite(value) ? Math.Max(0,value / 100) : 1;
            SoundChanged(nameof(SoundVolume));OnPropertyChanged(nameof(SoundVolumeSlider));
        }
    }
    public string SoundMode
    {
        get => SelectedSound?.Item.PlaybackMode ?? "Play once"; set
        {
            if (SelectedSound == null)
                return;
            SelectedSound.Item.PlaybackMode = value;
            SoundChanged(nameof(SoundMode));
        }
    }
    public string[] SoundModes { get; } = ["Play once", "Toggle", "Hold"];
    public string SoundFolderName
    {
        get => SelectedSound?.Item.Folder ?? ""; set
        {
            if (SelectedSound == null || string.IsNullOrEmpty(value))
                return;
            SelectedSound.Item.Folder = value;
            SelectedSound.Refresh();
            SoundChanged(nameof(SoundFolderName));
            SoundView.Refresh();
        }
    }
    private void SoundChanged(string property)
    {
        OnPropertyChanged(property);
        try
        {
            sounds.Save();
        }
        catch (Exception e) { Error(e); }
    }
    private void InitializeStudio()
    {
        sounds = new(store.Root);
        graph = new(sounds);
        graph.Error += StudioError;
        sounds.Error += StudioError;
        if (mic is MicrophoneService source)
            source.Samples += graph.PushMicrophone;
        graph.HearMyself = Settings.HearMyself;
        graph.SendMicrophone = Settings.SendMicrophone;
        graph.Muted = Muted;
        graph.MonitorVolume = (float)Math.Min(1e6,Settings.MonitoringVolume);
        graph.Processor.Settings = Settings.Processing;
        graph.Processor.Preset = Settings.ActiveAiVoice ? "Clean" : Settings.VoicePreset;
        graph.Processor.Enabled = Settings.VoiceEnabled;
        graph.Processor.Intensity = Settings.VoiceIntensity;
        sounds.AllowMultiple = Settings.AllowMultipleSounds;
        sounds.MasterVolume = (float)Math.Min(1e6,Settings.SoundboardVolume);
        foreach (var folder in sounds.Library.Folders)
            Folders.Add(folder);
        foreach (var sound in sounds.Library.Sounds)
            SoundCards.Add(new(sound));
        PresetView = CollectionViewSource.GetDefaultView(Presets);
        PresetView.Filter = o => o is PresetCard p && (!FavoritesOnly || Settings.FavoriteVoices.Contains(p.Name)) && (string.IsNullOrEmpty(VoiceSearch) || p.Name.Contains(VoiceSearch, StringComparison.OrdinalIgnoreCase));
        SoundView = CollectionViewSource.GetDefaultView(SoundCards);
        SoundView.Filter = o => o is SoundCard card && (!FavoriteSoundsOnly||Product.FavoriteSounds.Contains(card.Item.Id)) && (SelectedFolder == null || card.Folder == SelectedFolder.Name) && (string.IsNullOrWhiteSpace(Search) || card.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        RefreshOutputs();
        loadingStudio = false;
        ConfigureOutputs();
        InitializeAi();
        foreach(var p in Presets)p.Selected=p.Name==Settings.VoicePreset&&!Settings.ActiveAiVoice;
    }
    private void StudioError(string message) => Application.Current.Dispatcher.BeginInvoke(new Action(() => { store.Log(message); Status = message; }));
    [RelayCommand]
    private void RefreshOutputs()
    {
        bool prior = loadingStudio;
        try
        {
            var list = graph.Devices();
            loadingStudio = true;
            if (!OutputDevices.Skip(1).Select(d => d.Id).SequenceEqual(list.Select(d => d.Id)))
            {
                OutputDevices.Clear(); OutputDevices.Add(new("", "—"));
                foreach (var d in list) OutputDevices.Add(d);
            }
            MonitoringDevice = DeviceSelection.Physical(OutputDevices.ToArray(), Settings.MonitoringDeviceId) ?? OutputDevices.FirstOrDefault();
            VirtualDevice = DeviceSelection.Virtual(OutputDevices.ToArray(), Settings.VirtualDeviceId) ?? OutputDevices.FirstOrDefault();
            if (string.IsNullOrEmpty(Settings.MonitoringDeviceId) && !string.IsNullOrEmpty(MonitoringDevice?.Id)) Settings.MonitoringDeviceId = MonitoringDevice.Id;
            if (string.IsNullOrEmpty(Settings.VirtualDeviceId) && !string.IsNullOrEmpty(VirtualDevice?.Id)) Settings.VirtualDeviceId = VirtualDevice.Id;
        }
        catch (Exception e) { Error(e); }
        finally { loadingStudio = prior; }
        if (!prior) ConfigureOutputs();
    }
    private void ConfigureOutputs()
    {
        if (loadingStudio || !enableOutputs) return;
        try
        {
            graph.Configure(string.IsNullOrEmpty(MonitoringDevice?.Id) ? null : MonitoringDevice.Id, string.IsNullOrEmpty(VirtualDevice?.Id) ? null : VirtualDevice.Id);
            RouteStatus = graph.VirtualId == null ? T["RouteMissing"] : VirtualDevice!.Name + " · " + T["Connected"];
            if(lastRouteLog != RouteStatus) store.Log($"Routing: monitor={graph.MonitorId}; virtual={graph.VirtualId}; internal=48000Hz float stereo");
            lastRouteLog = RouteStatus;
            ScheduleSave();
        }
        catch (Exception e) { StudioError(e.Message); }
    }
    private string lastRouteLog = "";
    partial void OnMonitoringDeviceChanged(AudioDevice? value)
    { if(loadingStudio) return; Settings.MonitoringDeviceId=value?.Id; ConfigureOutputs(); }
    partial void OnVirtualDeviceChanged(AudioDevice? value)
    { if(loadingStudio) return; Settings.VirtualDeviceId=value?.Id; ConfigureOutputs(); }
    partial void OnSelectedFolderChanged(SoundFolder? value) => SoundView?.Refresh();
    partial void OnSearchChanged(string value) => SoundView?.Refresh();
    partial void OnSelectedSoundChanged(SoundCard? value)
    {
        NowPlayingVisible = value != null;
        foreach (var name in new[] { nameof(SoundHear), nameof(SoundSend), nameof(SoundLoop), nameof(SoundVolume), nameof(SoundVolumeSlider), nameof(SoundBass), nameof(SoundSaturation), nameof(SoundMode), nameof(SoundFolderName) })
            OnPropertyChanged(name);
    }
    partial void OnSoundPositionChanged(double value)
    {
        if (!updatingPosition && SelectedSound != null)
            sounds.Seek(SelectedSound.Item.Id, value);
    }
    private void TickStudio()
    {
        if (graph == null)
            return;
        ReleaseHeldSounds();
        ProcessedMeter = Math.Clamp((graph.ProcessedPeak + 60) / 60 * 100, 0, 100);
        MonitorMeter = Math.Clamp((graph.MonitorPeak + 60) / 60 * 100, 0, 100);
        OutputMeter = Math.Clamp((graph.OutputPeak + 60) / 60 * 100, 0, 100);
        if (SelectedSound != null)
        {
            var state = sounds.State(SelectedSound.Item.Id);
            updatingPosition = true;
            SoundDuration = Math.Max(1, state.Duration);
            SoundPosition = state.Position;
            updatingPosition = false;
            SoundTime = $"{TimeSpan.FromSeconds(state.Position):mm\\:ss} / {TimeSpan.FromSeconds(state.Duration):mm\\:ss}";
            SoundState = state.Paused ? T["Paused"] : state.Playing ? T["Playing"] : T["Stopped"];
        }
        TickAi();
    }
    [RelayCommand]
    private void SelectPreset(PresetCard? preset)
    {
        if (preset == null)
            return;
        StopLive(); Settings.ActiveAiVoice=false;
        graph.Processor.Designed=null;Settings.ActiveDesignedVoiceId=null;
        if(preset.Name!="Clean")Settings.LastDesignedVoiceId=null;
        Settings.VoicePreset = preset.Name;
        if(preset.Name!="Clean"){Settings.LastModifiedVoice=preset.Name;Settings.LastVoiceIsAi=false;}
        OnPropertyChanged(nameof(VoiceSwitchLabel));
        Settings.VoiceEnabled=true;graph.Processor.Enabled=true;
        OnPropertyChanged(nameof(VoiceEnabled));OnPropertyChanged(nameof(DisplayVoice));OnPropertyChanged(nameof(DisplayVoiceCover));
        graph.Processor.Preset = preset.Name;
        foreach(var p in Presets)p.Selected=p.Name==preset.Name;
        OnPropertyChanged(nameof(CurrentPreset));OnPropertyChanged(nameof(CurrentPresetDisplay));
        OnPropertyChanged(nameof(FavoritePreset));
        PresetView.Refresh();
        ScheduleSave();
    }
    [RelayCommand]
    private void AllSounds()
    {
        SelectedFolder = null;
        Search = "";
    }
    [RelayCommand]
    private async Task ImportSoundsAsync()
    {
        var dialog = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3;*.flac;*.m4a;*.ogg", Multiselect = true };
        if (dialog.ShowDialog() == true)
            await ImportPathsAsync(dialog.FileNames);
    }
    public async Task ImportPathsAsync(string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                var id = await sounds.ImportAsync(path, lifetime.Token);
                var item = sounds.Library.Sounds.First(s => s.Id == id);
                item.Folder = SelectedFolder?.Name ?? Folders.First().Name;
                SoundCards.Add(new(item));
                Status = T["Imported"] + " " + item.Name;
            }
            catch (Exception e) { StudioError(e.Message); }
        }
        sounds.Save();
    }
    [RelayCommand]
    private void PlaySound(SoundCard? sound)
    {
        if (sound == null)
            return;
        try
        {
            SelectedSound = sound;
            NowPlayingVisible = true;
            sounds.Play(sound.Item.Id);
            Settings.LastSoundId=sound.Item.Id;OnPropertyChanged(nameof(LastSoundName));OnPropertyChanged(nameof(LastSoundCover));ScheduleSave();
            store.Log($"Sound {sound.Item.Id}: hear={sound.Item.HearMyself}; send={sound.Item.SendToMic}; virtual={graph.VirtualId}");
            if (graph.MonitorId == null && graph.VirtualId == null)
                Status = T["ChooseOutput"];
        }
        catch (Exception e) { StudioError(e.Message); }
    }
    [RelayCommand]
    private void PauseSound()
    {
        if (SelectedSound == null)
            return;
        var state = sounds.State(SelectedSound.Item.Id);
        if (!state.Playing && !state.Paused)
            PlaySound(SelectedSound);
        else
            sounds.Pause(SelectedSound.Item.Id);
    }
    [RelayCommand]
    private void StopSound()
    {
        if (SelectedSound != null)
            sounds.Stop(SelectedSound.Item.Id);
    }
    [RelayCommand]
    private void RestartSound()
    {
        if (SelectedSound != null)
        {
            try
            {
                sounds.Restart(SelectedSound.Item.Id);
            }
            catch (Exception e) { StudioError(e.Message); }
        }
    }
    [RelayCommand] public void StopAllSounds() { sounds.StopAll(); StopPreview(); }
    [RelayCommand] private void ClosePlayer() => NowPlayingVisible = false;
    [RelayCommand] private void ResetSoundVolume() => SoundVolume = 100;
    [RelayCommand]
    private void ShowRouting()
    {
        Page = 6;
        Status = T["VirtualHelp"];
    }
    [RelayCommand] private void OpenCableWebsite() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true });
    private void DisposeStudio()
    {
        if (mic is MicrophoneService source)
            source.Samples -= graph.PushMicrophone;
        StopPreview();
        DisposeAi();
        graph.Dispose();
        sounds.Dispose();
    }
}
