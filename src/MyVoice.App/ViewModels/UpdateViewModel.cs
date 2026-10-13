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
    public string InstalledRelease => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??"2.6.0-beta.1";
    public string ApplicationVersion => "MyVoice · " + (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "2.6.0-beta.1");
    public string UpdateAvailability => "Releases officielles · téléchargement vérifié, installation et redémarrage avec retour à la version précédente en cas d’échec.";
    private UpdateSession? preparedUpdate;
    public bool AutomaticUpdates {get=>Settings.AutomaticUpdates;set{Settings.AutomaticUpdates=value;Changed(nameof(AutomaticUpdates));}}
    private string? downloadedUpdate;
    [ObservableProperty] private double updateProgress;
    public string LatestVersion=>string.IsNullOrEmpty(Settings.LastKnownVersion)?"Non vérifiée":Settings.LastKnownVersion;
    public string LastChecked=>Settings.LastUpdateCheck?.ToLocalTime().ToString("g")??"Jamais";
    public string[] UpdateChannels {get;}=["Stable","Beta"];
    public string UpdateChannel {get=>Settings.UpdateChannel=="beta"?"Beta":"Stable";set{Settings.UpdateChannel=value=="Beta"?"beta":"stable";CanInstallUpdate=false;downloadedUpdate=null;Changed(nameof(UpdateChannel));OnPropertyChanged(nameof(UpdateSource));}}
    public async Task CheckStartupUpdatesAsync()
    {
        if(Settings.LastUpdateSource==UpdateSource && Settings.LastUpdateCheck is {} checkedAt && DateTime.UtcNow-checkedAt<TimeSpan.FromHours(6) && Version.TryParse(Settings.LastKnownVersion,out _) && !ReleaseVersion.IsNewer(Settings.LastKnownVersion,Settings.LastKnownReleaseTag,InstalledRelease))
        {UpdateStatus="Dernière vérification : "+LastChecked+" · "+LatestVersion;return;}
        await CheckUpdatesCommand.ExecuteAsync(null);
    }
    [ObservableProperty] private string updateStatus = "";
    [ObservableProperty] private bool canInstallUpdate;
    public string UpdateSource
    {
        get => Settings.UpdateChannel=="beta"?DistributionDefaults.BetaManifestUrl:DistributionDefaults.ManifestUrl; set
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
            Settings.UiScale = Math.Clamp(value, .85, 1.5);
            Changed(nameof(UiScale));
        }
    }
    [ObservableProperty] private bool updateIndeterminate;
    [ObservableProperty] private string releaseNotesUrl="https://github.com/mattheube/MyVoice/releases";
    private void ShowUpdateState(UpdateSnapshot value){UpdateStatus=value.Message;UpdateProgress=value.Percent??0;UpdateIndeterminate=value.Percent==null && value.Phase is UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Verifying;}
    public void AdoptUpdateSession(UpdateSession session)
    {
        if(session.Source!=UpdateSource)return;
        preparedUpdate=session;
        ShowUpdateState(session.State);downloadedUpdate=session.DownloadedFile;CanInstallUpdate=downloadedUpdate!=null;
        if(CanInstallUpdate)RecordNotification(session.State.Message,"updates");
        if(session.Release is {} release && session.CheckedAt is {} date)
        {
            Settings.LastUpdateCheck=date;Settings.LastKnownVersion=release.Version;Settings.LastKnownReleaseTag=release.Tag;Settings.LastUpdateSource=UpdateSource;
            ReleaseNotesUrl=release.ReleaseUrl;OnPropertyChanged(nameof(LatestVersion));OnPropertyChanged(nameof(LastChecked));Save();
        }
    }
    [RelayCommand] private void OpenReleaseNotes()
    {
        // The application only accepts release URLs under its fixed official repository.
        var url=ReleaseNotesUrl.StartsWith("https://github.com/"+DistributionDefaults.Repository+"/releases/",StringComparison.Ordinal)?ReleaseNotesUrl:"https://github.com/"+DistributionDefaults.Repository+"/releases";
        Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    }
    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        CanInstallUpdate=false;downloadedUpdate=null;
        var session=new UpdateSession();
        var source=UpdateSource;
        await session.RunAsync(source,InstalledRelease,Path.Combine(store.Root,"cache","updates"),new Progress<UpdateSnapshot>(value=>{if(source==UpdateSource)ShowUpdateState(value);}),store.Log,lifetime.Token);
        AdoptUpdateSession(session);
    }
    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if(downloadedUpdate==null||!CanInstallUpdate)return;
        try
        {
            if(preparedUpdate?.Release is not {} release||string.IsNullOrEmpty(release.PackageUrl))
            {Process.Start(new ProcessStartInfo("explorer.exe"){Arguments="/select,\""+downloadedUpdate+"\"",UseShellExecute=true});return;}
            store.Save(Settings);mixerStore?.Save(mixerState);UpdateStatus="Préparation de l’installation…";
            await UpdateHandoff.LaunchAsync(AppContext.BaseDirectory,downloadedUpdate,preparedUpdate.Source,release,lifetime.Token);
            ((App)System.Windows.Application.Current).ExitApplication();
        }
        catch(Exception e){UpdateStatus="Installation impossible : "+e.Message;Error(e);}
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
