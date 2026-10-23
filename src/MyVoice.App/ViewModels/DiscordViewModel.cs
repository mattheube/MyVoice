using System;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Infrastructure;

namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private readonly DiscordPresence discordPresence=new();
    public bool ShareDiscordPresence
    {
        get=>Product.DiscordPresence;
        set{if(Product.DiscordPresence==value)return;Product.DiscordPresence=value;SaveProduct();discordPresence.SetEnabled(value);OnPropertyChanged();OnPropertyChanged(nameof(DiscordPresenceLabel));}
    }
    public string DiscordPresenceLabel=>T[discordPresence.Status switch
    {DiscordPresenceStatus.Connected=>"DiscordPresenceConnected",DiscordPresenceStatus.Waiting=>"DiscordPresenceWaiting",_=>"DiscordPresenceOff"}];
    private void InitializeDiscord()
    {
        discordPresence.StatusChanged+=DiscordStatusChanged;
        if(!Environment.GetCommandLineArgs().Contains("--smoke-test"))discordPresence.SetEnabled(Product.DiscordPresence);
    }
    private void DiscordStatusChanged(DiscordPresenceStatus status)
    {
        var dispatcher=Application.Current?.Dispatcher;
        if(dispatcher==null||dispatcher.HasShutdownStarted)return;
        dispatcher.BeginInvoke(()=>OnPropertyChanged(nameof(DiscordPresenceLabel)));
    }
    private void DisposeDiscord(){discordPresence.StatusChanged-=DiscordStatusChanged;discordPresence.Dispose();}
    [RelayCommand(IncludeCancelCommand=true)]
    private async Task SignInDiscordAsync(CancellationToken cancellationToken)
    {
        if(CommunityBusy)return;
        CommunityBusy=true;AccountStatus=T["DiscordSignInPending"];
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,lifetime.Token);
        try
        {
            await community.SignInWithDiscord(linked.Token);
            OnPropertyChanged(nameof(SignedIn));OnPropertyChanged(nameof(AccountLabel));NotifyPublication();
            await RefreshPublicProfileAsync();AccountStatus=T["Runtime26"];
        }
        catch(OperationCanceledException){AccountStatus=T["DiscordSignInCancelled"];}
        catch(Exception e){AccountStatus=e.Message;}
        finally{CommunityBusy=false;}
    }
}
