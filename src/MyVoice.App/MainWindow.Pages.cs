using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MyVoice.App.Services;
using MyVoice.App.Views;
namespace MyVoice.App;
public partial class MainWindow
{
    private readonly Dictionary<int,(UserControl View,DateTime Used)> pages=new();
    private readonly DispatcherTimer pageMaintenance=new(){Interval=TimeSpan.FromSeconds(30)};
    private DateTime hiddenSince=DateTime.UtcNow;
    private void InitializePages()
    {
        Activated+=ActivityChanged;Deactivated+=ActivityChanged;StateChanged+=ActivityChanged;
        IsVisibleChanged+=(_,_)=>ActivityChanged(this,EventArgs.Empty);
        pageMaintenance.Tick+=MaintainPages;pageMaintenance.Start();
        Closed+=(_,_)=>{pageMaintenance.Stop();pageMaintenance.Tick-=MaintainPages;pages.Clear();};
        ShowPage();
    }
    private void ActivityChanged(object? sender,EventArgs e)
    {
        var state=!IsVisible?UiActivityState.Tray:WindowState==WindowState.Minimized||!IsActive?UiActivityState.Background:UiActivityState.Foreground;
        if(state!=UiActivity.State&&state!=UiActivityState.Foreground)hiddenSince=DateTime.UtcNow;
        UiActivity.Set(state);
        if(IsVisible&&WindowState!=WindowState.Minimized&&PageHost.Children.Count==0)ShowPage();
    }
    private void ShowPage()
    {
        int page=vm.Page;
        if(!pages.TryGetValue(page,out var cached))
        {
            UserControl view=page switch{0=>new VoiceView(),1=>new SoundboardView(),2=>new StudioView(),3=>new SettingsView(),4=>new HomeView(),5=>new CommunityView(),6=>new MicSetupView(),7=>new ProfileView(),8=>new MixerView(),9=>new PublicationView(),_=>new HomeView()};
            view.DataContext=vm; // Keep bindings stable while a cached page is detached.
            cached=(view,DateTime.UtcNow);
        }
        PageHost.Children.Clear();PageHost.Children.Add(cached.View);pages[page]=(cached.View,DateTime.UtcNow);
        // Profile keeps unsubmitted form fields. Other state belongs to the shared VM/services.
        foreach(var old in pages.Where(p=>p.Key!=page&&p.Key!=7).OrderByDescending(p=>p.Value.Used).Skip(7).Select(p=>p.Key).ToArray())pages.Remove(old);
    }
    private void MaintainPages(object? sender,EventArgs e)
    {
        var memory=GC.GetGCMemoryInfo();
        if(memory.HighMemoryLoadThresholdBytes>0&&memory.MemoryLoadBytes>memory.HighMemoryLoadThresholdBytes*0.9)ThumbnailCache.Clear();
        var cutoff=DateTime.UtcNow-TimeSpan.FromSeconds(90);
        foreach(var old in pages.Where(p=>p.Key!=vm.Page&&p.Key!=7&&p.Value.Used<cutoff).Select(p=>p.Key).ToArray())pages.Remove(old);
        if(UiActivity.State==UiActivityState.Tray&&hiddenSince<cutoff&&vm.Page!=7){PageHost.Children.Clear();pages.Remove(vm.Page);ThumbnailCache.Clear();}
    }
}
