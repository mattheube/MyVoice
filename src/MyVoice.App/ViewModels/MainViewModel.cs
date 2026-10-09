using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Core;
using MyVoice.Infrastructure;
using MyVoice.App.Services;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel : ObservableObject, IDisposable
{
    public Texts T { get; } = new();
    public AppSettings Settings
    {
        get;
    }
    private readonly bool enableOutputs;
    private readonly LocalStore store;
    private readonly IMicrophoneService mic;
    private readonly DispatcherTimer timer;
    private readonly DispatcherTimer saveTimer;
    private readonly CancellationTokenSource lifetime = new();
    private HotkeyService? hotkey;
    private CalibrationResult? recommendation;
    public ObservableCollection<AudioDevice> Devices { get; } = new();
    [ObservableProperty] private AudioDevice? selectedDevice;
    [ObservableProperty] private int page;
    [ObservableProperty] private bool running;
    [ObservableProperty] private bool calibrating;
    [ObservableProperty] private bool canApply;
    [ObservableProperty] private bool clipping;
    [ObservableProperty] private double meter;
    [ObservableProperty] private string levelText = "−90 dB";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string calibrationText = "";
    [ObservableProperty] private double gainDb;
    [ObservableProperty] private bool muted;
    public string GainLabel => $"{GainDb:+0.0;-0.0;0.0} dB  ·  {Math.Pow(10, GainDb / 20) * 100:0} %";
    public string MicState => !Running ? T["Idle"] : Muted ? T["Muted"] : T["Active"];
    public string StartLabel => T[Running ? "Stop" : "Start"];
    public string HotkeyLabel => ReadShortcutLabel();
    public bool CloseToTray
    {
        get => Settings.CloseToTray; set
        {
            Settings.CloseToTray = value;
            Changed(nameof(CloseToTray));
        }
    }
    public bool StartMinimized
    {
        get => Settings.StartMinimized; set
        {
            Settings.StartMinimized = value;
            Changed(nameof(StartMinimized));
        }
    }
    public bool Animations
    {
        get => Settings.Animations; set
        {
            Settings.Animations = value;
            Changed(nameof(Animations));
        }
    }
    public bool LaunchAtStartup
    {
        get => Settings.LaunchAtStartup; set
        {
            try
            {
                StartupService.Set(value);
                Settings.LaunchAtStartup = value;
                Changed(nameof(LaunchAtStartup));
            }
            catch (Exception e) { Error(e); OnPropertyChanged(); }
        }
    }
    public int LanguageIndex
    {
        get => Settings.Language == "fr" ? 0 : 1; set
        {
            Settings.Language = value == 0 ? "fr" : "en";
            T.Load(Settings.Language);
            Changed(nameof(LanguageIndex));
            OnPropertyChanged(nameof(MicState));
            OnPropertyChanged(nameof(StartLabel));
            OnPropertyChanged(nameof(HotkeyLabel));
        }
    }
    public MainViewModel(LocalStore store, IMicrophoneService mic, bool enableOutputs = true)
    {
        this.enableOutputs = enableOutputs;
        this.store = store;
        this.mic = mic;
        Settings = store.Load();
        T.Load(Settings.Language);
        gainDb = Settings.GainDb;
        muted = Settings.Muted;
        mic.GainDb = gainDb;
        mic.Muted = muted;
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) => Tick();
        InitializeStudio();
        InitializeProduct();
        timer.Start();
        mic.Faulted += OnFault;
        mic.DevicesChanged += OnDevicesChanged;
        Refresh();
        if (store.RecoveryMessage != null)
            Status = T["Recovered"];
    }
    private void OnDevicesChanged()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (!lifetime.IsCancellationRequested) { Refresh(); RefreshOutputs(); } }));
    }
    private void OnFault(string message)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { store.Log("Audio fault: " + message); Status = T["Disconnected"]; Running = false; }));
    }
    private void Tick()
    {
        if (Running && !mic.IsRunning)
        {
            Running = false;
            Status = T["Disconnected"];
        }
        var level = mic.Level;
        Meter = Math.Clamp((level.PeakDb + 60) / 60 * 100, 0, 100);
        LevelText = $"Peak {level.PeakDb:0.0} dB   ·   RMS {level.AverageDb:0.0} dB";
        Clipping = level.Clipping;
        TickStudio();
        TickRecovery();
    }
    partial void OnRunningChanged(bool value)
    {
        if (!value && graph != null) {resumeAiAfterReconnect=AiLive;StopLive();}
        OnPropertyChanged(nameof(MicState));
        OnPropertyChanged(nameof(StartLabel));
        CalibrateCommand.NotifyCanExecuteChanged();
        ToggleMuteCommand.NotifyCanExecuteChanged();
    }
    partial void OnCalibratingChanged(bool value)
    {
        CalibrateCommand.NotifyCanExecuteChanged();
        ToggleCaptureCommand.NotifyCanExecuteChanged();
    }
    partial void OnSelectedDeviceChanged(AudioDevice? value)
    {
        if (refreshingDevices) return;
        if (value != null) Settings.InputDeviceId = value.Id;
        if (Running)
        {
            mic.Stop();
            graph?.FlushMicrophone();
            Running = false;
            Status = T["DeviceHint"];
        }
        ScheduleSave();
        if (automaticAudio) EnsureMicrophone();
        ToggleCaptureCommand.NotifyCanExecuteChanged();
    }
    partial void OnGainDbChanged(double value)
    {
        mic.GainDb = value;
        Settings.GainDb = value;
        OnPropertyChanged(nameof(GainLabel));
        ScheduleSave();
    }
    partial void OnMutedChanged(bool value)
    {
        mic.Muted = value;
        Settings.Muted = value;
        System.Threading.Interlocked.Increment(ref aiGeneration);
        aiInput.Clear();
        if (graph != null)
        {
            graph.Muted = value;
            graph.FlushMicrophone();
        }
        OnPropertyChanged(nameof(MicState));
        ScheduleSave();
    }
    private void Changed(string name)
    {
        OnPropertyChanged(name);
        ScheduleSave();
    }
    private void ScheduleSave()
    {
        if (saveTimer != null)
        {
            saveTimer.Stop();
            saveTimer.Start();
        }
    }
    public void Save()
    {
        try
        {
            store.Save(Settings);
        }
        catch (Exception e) { Error(e); }
    }
    private void Error(Exception e)
    {
        store.Log(e.ToString());
        Status = T["Error"];
    }
    [RelayCommand]
    private void Navigate(string target)
    {
        if (int.TryParse(target, out var n))
            Page = n;
    }
    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var available = mic.GetDevices();
            refreshingDevices = true;
            try
            {
                foreach (var removed in Devices.Where(d => !available.Any(a => a.Id == d.Id)).ToArray()) Devices.Remove(removed);
                foreach (var device in available) if (!Devices.Any(d => d.Id == device.Id)) Devices.Add(device);
                var selected = DeviceSelection.Physical(available, Settings.InputDeviceId);
                if (SelectedDevice?.Id != selected?.Id) { mic.Stop(); Running = false; }
                SelectedDevice = Devices.FirstOrDefault(d => d.Id == selected?.Id);
                if (string.IsNullOrEmpty(Settings.InputDeviceId) && SelectedDevice != null) Settings.InputDeviceId = SelectedDevice.Id;
            }
            finally { refreshingDevices = false; }
            if (automaticAudio) EnsureMicrophone();
            if (Devices.Count == 0) Status = T["NoInput"];
            store.Log($"Device enumeration: {Devices.Count} active capture devices");
        }
        catch (Exception e) { Error(e); }
    }
    private bool CanCapture() => SelectedDevice != null && !Calibrating;
    [RelayCommand(CanExecute = nameof(CanCapture))]
    private void ToggleCapture()
    {
        try
        {
            if (Running)
            {
                mic.Stop();
                graph?.FlushMicrophone();
                Running = false;
                Status = T["Idle"];
            }
            else
            {
                var id = SelectedDevice!.Id;
                mic.Start(id);
                Running = mic.IsRunning;
                Status = T["CaptureInfo"];
            }
        }
        catch (Exception e) { Running = false; Error(e); }
    }
    private bool CanMute() => Running;
    [RelayCommand(CanExecute = nameof(CanMute))] public void ToggleMute() => Muted = !Muted;
    [RelayCommand] private void Restore() => GainDb = 0;
    private bool CanCalibrate() => Running && !Calibrating;
    [RelayCommand(CanExecute = nameof(CanCalibrate))]
    private async Task CalibrateAsync()
    {
        Calibrating = true;
        CanApply = false;
        CalibrationText = T["Calibrating"];
        try
        {
            recommendation = await mic.CalibrateAsync(lifetime.Token);
            CanApply = recommendation.CanApply;
            CalibrationText = CanApply ? $"{T["CalibrationReady"]} : {recommendation.GainDb:+0.0;-0.0;0.0} dB · noise {recommendation.NoiseDb:0} dB · voice {recommendation.VoiceDb:0} dB" : T[recommendation.Reason == "clipping" ? "CalibrationClip" : recommendation.Reason == "noise" ? "CalibrationNoise" : "CalibrationInsufficient"];
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Error(e); }
        finally { Calibrating = false; }
    }
    [RelayCommand]
    private void ApplyCalibration()
    {
        if (CanApply && recommendation != null)
            GainDb = recommendation.GainDb;
        CanApply = false;
        CalibrationText = "";
    }
    [RelayCommand]
    private void KeepCalibration()
    {
        CanApply = false;
        CalibrationText = "";
    }
    public void AttachHotkey(HotkeyService service)
    {
        hotkey = service;
        service.Pressed += OnHotkey;
        AttachStudioHotkeys(service);
        if (!service.Set(Settings.HotkeyModifiers, Settings.HotkeyKey))
            Status = T["Conflict"];
    }
    private void OnHotkey()
    {
        if (Running)
            ToggleMute();
    }
    public bool SetHotkey(uint mods, uint key)
    {
        if (ShortcutAction == "none" || hotkey == null || !hotkey.SetAction(ShortcutAction, mods, key))
        {
            Status = T["Conflict"];
            return false;
        }
        if (ShortcutAction == "mute")
        {
            Settings.HotkeyModifiers = mods;
            Settings.HotkeyKey = key;
        }
        else
        {
            if (key == 0)
                Settings.Shortcuts.Remove(ShortcutAction);
            else
                Settings.Shortcuts[ShortcutAction] = new(mods, key);
        }
        OnPropertyChanged(nameof(HotkeyLabel));
        Save();
        return true;
    }
    [RelayCommand] private void ClearHotkey() => SetHotkey(0, 0);
    [RelayCommand] private void OpenData() => OpenFolder(store.Root);
    [RelayCommand] private void OpenLogs() => OpenFolder(Path.Combine(store.Root, "logs"));
    private void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception e) { Error(e); }
    }
    public void Dispose()
    {
        lifetime.Cancel();
        timer.Stop();
        saveTimer.Stop();
        if (hotkey != null)
            hotkey.Pressed -= OnHotkey;
        mic.Faulted -= OnFault;
        mic.DevicesChanged -= OnDevicesChanged;
        community?.Dispose();DisposeStudio();
        mic.Dispose();
        Save();
    }
}
