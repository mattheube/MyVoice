using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Audio;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private readonly DispatcherTimer applicationRefresh=new(){Interval=TimeSpan.FromSeconds(5)};
    private bool discoveringApplications;
    private ListCollectionView? applicationView;
    public ListCollectionView ApplicationView
    {
        get
        {
            if(applicationView==null){applicationView=new(MixerApplications);applicationView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ApplicationAudioSource.Group)));applicationView.Filter=x=>x is ApplicationAudioSource a&&(a.Label+" "+a.Name+" "+a.ExecutablePath).Contains(ApplicationSearch,StringComparison.OrdinalIgnoreCase);}
            return applicationView;
        }
    }
    [ObservableProperty] private string applicationSearch="";
    partial void OnApplicationSearchChanged(string value)=>applicationView?.Refresh();
    private void InitializeApplicationDiscovery(){applicationRefresh.Tick+=PollApplications;applicationRefresh.Start();}
    private async void PollApplications(object? sender,EventArgs e){if(Page==8&&Services.UiActivity.VisualsActive)await RefreshApplicationsAsync();}
    [RelayCommand] private async Task RefreshApplicationsAsync()
    {
        if(discoveringApplications)return;discoveringApplications=true;
        try
        {
            var saved=mixerState.Current.Channels.ToArray();
            var found=await Task.Run(()=>ApplicationDiscoveryService.Shared.Discover(saved).ToArray());
            if(lifetime.IsCancellationRequested)return;
            mixerApplicationCache=found;RefreshMixerSourceChoices();
        }
        catch(Exception e){MixerStatus="Détection des applications : "+e.Message;}
        finally{discoveringApplications=false;}
    }
    [RelayCommand] private async Task ForceRefreshApplicationsAsync()
    {
        if(discoveringApplications)return;
        try{var saved=mixerState.Current.Channels.ToArray();await Task.Run(()=>ApplicationDiscoveryService.Shared.Discover(saved,force:true));await RefreshApplicationsAsync();}
        catch(Exception e){MixerStatus="Détection des applications : "+e.Message;}
    }
    [RelayCommand] private void ChooseMixerApplication()
    {
        var picker=new Microsoft.Win32.OpenFileDialog{Title="Choisir une application",Filter="Applications (*.exe)|*.exe|Tous les fichiers|*.*"};
        if(picker.ShowDialog()!=true)return;
        try
        {
            var item=ApplicationDiscoveryService.FromFile(picker.FileName);
            mixerApplicationCache=ApplicationDiscoveryService.Merge(mixerApplicationCache.Append(item));ApplicationSearch="";RefreshMixerSourceChoices();MixerApplication=MixerApplications.First(a=>a.Identity.Equals(item.Identity,StringComparison.OrdinalIgnoreCase));
            MixerStatus="Application choisie. Ajoutez la source puis activez Capture pour appliquer ses règles à son ouverture.";
        }
        catch(Exception e){MixerStatus=e.Message;}
    }
}
