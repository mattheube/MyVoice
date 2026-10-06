using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Updater;
using MyVoice.App.Services;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    public string ApplicationVersion => "MyVoice · " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.1");
    public string UpdateAvailability => string.IsNullOrWhiteSpace(UpdateSource) ? "Les mises à jour automatiques seront activées lorsque le dépôt de publication sera configuré." : "Distribution GitHub Releases · téléchargement vérifié, installation manuelle.";
    private string? downloadedUpdate;
    [ObservableProperty] private double updateProgress;
    public string LatestVersion=>string.IsNullOrEmpty(Settings.LastKnownVersion)?"Non vérifiée":Settings.LastKnownVersion;
    public string LastChecked=>Settings.LastUpdateCheck?.ToLocalTime().ToString("g")??"Jamais";
    public string UpdateChannel=>"Stable";
    public async Task CheckStartupUpdatesAsync()
    {
        if(Settings.LastUpdateSource==UpdateSource && Settings.LastUpdateCheck is {} checkedAt && DateTime.UtcNow-checkedAt<TimeSpan.FromHours(6) && Version.TryParse(Settings.LastKnownVersion,out var last) && last<=(Assembly.GetExecutingAssembly().GetName().Version??new Version(0,0)))
        {UpdateStatus="Dernière vérification : "+LastChecked+" · "+LatestVersion;return;}
        await CheckUpdatesCommand.ExecuteAsync(null);
    }
    [ObservableProperty] private string updateStatus = "";
    [ObservableProperty] private bool canInstallUpdate;
    public string UpdateSource
    {
        get => string.IsNullOrWhiteSpace(Settings.UpdateManifestUrl)?DistributionDefaults.ManifestUrl:Settings.UpdateManifestUrl; set
        {
            Settings.UpdateManifestUrl = value;
            Changed(nameof(UpdateSource));
        }
    }
    public bool CheckUpdatesAtStartup
    {
        get => Settings.CheckUpdatesAtStartup; set
        {
            Settings.CheckUpdatesAtStartup = value;
            Changed(nameof(CheckUpdatesAtStartup));
        }
    }
    public bool Notifications
    {
        get => Settings.Notifications; set
        {
            Settings.Notifications = value;
            Changed(nameof(Notifications));
        }
    }
    public double UiScale
    {
        get => Settings.UiScale; set
        {
            Settings.UiScale = Math.Clamp(value, .85, 1.3);
            Changed(nameof(UiScale));
        }
    }
    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        CanInstallUpdate = false;
        downloadedUpdate = null;
        if (string.IsNullOrWhiteSpace(UpdateSource))
        {
            UpdateStatus = T["NoUpdateSource"];
            return;
        }
        try
        {
            UpdateProgress=0;UpdateStatus="Recherche de mises à jour…";
            using var updater=new UpdateService();
            var release=await updater.ReadManifestAsync(UpdateSource,lifetime.Token);
            var current=Assembly.GetExecutingAssembly().GetName().Version??new Version(0,0);
            Settings.LastUpdateCheck=DateTime.UtcNow;Settings.LastKnownVersion=release.Version;Settings.LastUpdateSource=UpdateSource;
            OnPropertyChanged(nameof(LatestVersion));OnPropertyChanged(nameof(LastChecked));Save();
            if(Version.Parse(release.Version)<=current){UpdateStatus="À jour · "+release.Version;return;}
            if(current<Version.Parse(release.MinimumVersion)){UpdateStatus="Version intermédiaire requise : "+release.MinimumVersion;return;}
            UpdateStatus="Téléchargement de MyVoice "+release.Version;
            downloadedUpdate=await updater.DownloadAsync(release,UpdateSource,Path.Combine(store.Root,"cache","updates"),lifetime.Token,
                new Progress<double>(value=>UpdateProgress=value*100),new Progress<DownloadProgress>(p=>UpdateStatus=$"MyVoice {release.Version} · {p.Received/1048576d:0.0} Mo"+(p.Total>0?$" / {p.Total/1048576d:0.0} Mo · {p.Received*100d/p.Total:0}%":"")));
            CanInstallUpdate=true;UpdateStatus="MyVoice "+release.Version+" téléchargé et vérifié. Ouvrez le dossier pour lancer l’installation manuellement.";
        }
        catch(OperationCanceledException){UpdateStatus="Vérification ou téléchargement interrompu. La version installée reste utilisable.";}
        catch(Exception e){UpdateStatus="Mise à jour indisponible : "+e.Message;store.Log(e.ToString());}
    }
    [RelayCommand]
    private void InstallUpdate()
    {
        if(downloadedUpdate==null||!CanInstallUpdate)return;
        try{Process.Start(new ProcessStartInfo("explorer.exe"){Arguments="/select,\""+downloadedUpdate+"\"",UseShellExecute=true});}
        catch(Exception e){Error(e);}
    }
    [RelayCommand]
    private void ResetAll()
    {
        if (!Dialogs.Confirm(T["ResetAll"] + " ?"))
            return;
        ResetProcessing();
        MonitoringVolume = 50;
        SoundboardVolume = 100;
        VoiceIntensity = 100;
        HearMyself = false;
        VoiceEnabled = true;
        SelectPreset(Presets[0]);
        AiProfile = "Balanced";
        Save();
    }
}
