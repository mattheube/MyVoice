using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.Infrastructure;
namespace MyVoice.App.ViewModels;

public sealed partial class CommunityCard : ObservableObject
{
    public string Id {get;init;}="";
    public string Title {get;init;}="";
    public string Description {get;init;}="";
    public string Kind {get;init;}="";
    public string Owner {get;init;}="";
    public string Username {get;init;}="";
    public bool IsVoice=>Kind!="soundboard";
    public string Shape=>IsVoice?"circle":"square";
    public string CreatorLabel=>Official?"MyVoice ✓":"@"+Username;
    public string Visibility {get;init;}="public";
    public string[] Tags {get;init;}=[];
    public bool Official {get;init;}
    [ObservableProperty] private bool liked;
    [ObservableProperty] private bool installed;
    public bool CoverRequested {get;set;}
    [ObservableProperty] private bool coverLoading;
    [ObservableProperty] private bool coverFailed;
    [ObservableProperty] private string? cover;
    [ObservableProperty] private double progress;
    [ObservableProperty] private string state="Installer";
}
public sealed record SocialContact(string Id,string Name,string Username,string Kind);
public sealed record CreatorProfile(string Id,string Username,string DisplayName,string Bio,bool Verified,string Relationship,string? Avatar=null,string? Banner=null);
public partial class MainViewModel
{
    private CommunityClient community=null!;
    public bool CommunityConfigured=>community?.Configured==true;
    public bool SignedIn=>community?.Session!=null;
    [ObservableProperty] private bool communityBusy;
    [ObservableProperty] private string communityStatus="Community hors ligne · service non connecté.";
    [ObservableProperty] private string communitySearch="";
    [ObservableProperty] private string communityKind="all";
    [ObservableProperty] private string communitySort="Recent";
    [ObservableProperty] private string accountStatus="Votre profil local fonctionne sans compte.";
    [ObservableProperty] private bool profilePrivate;
    [ObservableProperty] private CommunityCard? selectedCreation;
    [ObservableProperty] private CreatorProfile? selectedCreator;
    [ObservableProperty] private string publishDescription="";
    [ObservableProperty] private string publishVisibility="private";
    [ObservableProperty] private bool publishRights;
    public string[] ContentKinds {get;}=["all","soundboard","voice-preset","ai-voice"];
    public string[] ContentSorts {get;}=["Recent","Popular"];
    public string[] Visibilities {get;}=["private","unlisted","friends","public"];
    public ObservableCollection<CommunityCard> CommunityItems {get;}=[];
    public ObservableCollection<SocialContact> SocialContacts {get;}=[];
    [ObservableProperty] private string socialCounts="";
    [RelayCommand] private async Task RefreshSocialAsync(){try{var data=await community.Rpc("social_overview",new{});SocialContacts.Clear();foreach(var type in new[]{"friends","requests"})foreach(var p in data.GetProperty(type).EnumerateArray())SocialContacts.Add(new(p.GetProperty("id").GetString()!,p.GetProperty("name").GetString()!,p.GetProperty("username").GetString()!,p.GetProperty("kind").GetString()!));SocialCounts=$"{data.GetProperty("followers")} abonnés · {data.GetProperty("following")} suivis";}catch(Exception e){AccountStatus=e.Message;}}
    [RelayCommand] private async Task AcceptFollowAsync(SocialContact? contact)=>await AnswerFollow(contact,true);
    [RelayCommand] private async Task DeclineFollowAsync(SocialContact? contact)=>await AnswerFollow(contact,false);
    private async Task AnswerFollow(SocialContact? contact,bool accept){if(contact==null||contact.Kind!="request")return;try{await community.Rpc("answer_follow",new{requester=contact.Id,accept});await RefreshSocialAsync();ShowToast(accept?T["Runtime36"]:T["Runtime37"]);}catch(Exception e){AccountStatus=e.Message;}}

    private void InitializeCommunity()
    {
        community=new(CommunityConfiguration.Load(Path.Combine(AppContext.BaseDirectory,"community.public.json")),store.Root);
        SoundCards.CollectionChanged+=(_,_)=>RefreshCommunityLibraryState();
        AiVoices.CollectionChanged+=(_,_)=>RefreshCommunityLibraryState();
        DesignedVoices.CollectionChanged+=(_,_)=>RefreshCommunityLibraryState();
        CommunityItems.CollectionChanged+=(_,_)=>RefreshCommunityLibraryState();
        OwnPublications.CollectionChanged+=(_,_)=>RefreshCommunityLibraryState();
        InitializePublication();
        if(CommunityConfigured){CommunityStatus=T["Runtime79"];AccountStatus=SignedIn?T["Runtime96"]:T["Runtime23"];}
    }
    private void RefreshCommunityLibraryState()
    {
        var installed=SoundCards.Select(c=>c.Item.SourceContentId)
            .Concat(AiVoices.Select(v=>v.SourceContentId))
            .Concat(DesignedVoices.Select(v=>v.SourceContentId))
            .Where(id=>!string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
        foreach(var card in CommunityItems.Concat(OwnPublications))card.Installed=installed.Contains(card.Id);
    }
    public async Task AccountAction(string action,string email,string username,string display,string password,string confirmation)
    {
        if(CommunityBusy)return;CommunityBusy=true;
        try
        {
            if(action=="register"){await community.Register(email,username,display,password,confirmation);AccountStatus=T["Runtime119"];}
            else if(action=="recover"){await community.Recover(email);AccountStatus=T["Runtime97"];}
            else {await community.Login(email,password);var own=(await community.OwnProfile()).EnumerateArray().FirstOrDefault();if(own.ValueKind==JsonValueKind.Object){ProfilePrivate=own.GetProperty("private").GetBoolean();ProfileName=own.GetProperty("display_name").GetString()!;ProfileBio=own.GetProperty("bio").GetString()!;}AccountStatus=T["Runtime26"];}
            OnPropertyChanged(nameof(SignedIn));OnPropertyChanged(nameof(AccountLabel));NotifyPublication();if(SignedIn)await RefreshPublicProfileAsync();
        }catch(Exception e){AccountStatus=e.Message;}finally{CommunityBusy=false;}
    }
    [RelayCommand] private async Task LogoutAsync(){try{await community.Logout();}catch{}finally{OwnPublications.Clear();ProfileEmail="";OnPropertyChanged(nameof(SignedIn));OnPropertyChanged(nameof(AccountLabel));NotifyPublication();if(SignedIn)await RefreshPublicProfileAsync();AccountStatus=T["Runtime41"];}}
    [RelayCommand] private async Task SaveOnlineProfileAsync(){try{await community.UpdateProfile(ProfileName,ProfileBio,ProfilePrivate,ProfileUsername,await ProfileAvatarData(),ProfileBannerData());ShowToast(T["Runtime72"]);}catch(Exception e){AccountStatus=e.Message;}}
    [RelayCommand] private async Task RefreshCommunityAsync()
    {
        if(CommunityBusy)return;CommunityBusy=true;CommunityStatus=T["Runtime86"];
        try{var feed=await community.Feed(CommunitySearch,CommunityKind,CommunitySort);CommunityItems.Clear();foreach(var item in feed.EnumerateArray())CommunityItems.Add(ReadCommunityCard(item));CommunityStatus=CommunityItems.Count==0?T["Runtime5"]:CommunityItems.Count+T["Runtime122"];}catch(Exception e){CommunityStatus=e.Message;}finally{CommunityBusy=false;}
    }
    [RelayCommand] private async Task FindCreatorAsync()
    {
        try{var p=await community.Rpc("creator_by_username",new{handle=CommunitySearch.TrimStart('@')});SelectedCreator=p.ValueKind==JsonValueKind.Null?null:new(p.GetProperty("id").GetString()!,p.GetProperty("username").GetString()!,p.GetProperty("display_name").GetString()!,p.GetProperty("bio").GetString()!,p.GetProperty("verified").GetBoolean(),p.GetProperty("relationship").GetString()!,CacheCreatorImage(p,"avatar"),CacheCreatorImage(p,"banner"));CommunityStatus=SelectedCreator==null?T["Runtime32"]:T["Runtime69"];}catch(Exception e){CommunityStatus=e.Message;}
    }
    [RelayCommand] private async Task FollowCreatorAsync(){if(SelectedCreator==null)return;try{var result=await community.Rpc("follow_creator",new{target=SelectedCreator.Id,enabled=SelectedCreator.Relationship is not("Friends" or "Following")});SelectedCreator=SelectedCreator with{Relationship=result.GetString()!};ShowToast(SelectedCreator.Relationship=="Friends"?T["Runtime118"]:SelectedCreator.Relationship);}catch(Exception e){CommunityStatus=e.Message;}}
    [RelayCommand] private async Task BlockCreatorAsync(){if(SelectedCreator==null)return;try{await community.Rpc("block_creator",new{target=SelectedCreator.Id,enabled=true});SelectedCreator=null;ShowToast(T["Runtime114"]);}catch(Exception e){CommunityStatus=e.Message;}}
    [RelayCommand] private async Task LikeCreationAsync()=>await React("like");
    [RelayCommand] private async Task SaveCreationAsync()=>await React("save");
    private async Task React(string action){if(SelectedCreation is not {} card)return;try{await community.Rpc("react_to_content",new{target=card.Id,action,enabled=action=="like"?!card.Liked:true});if(action=="like")card.Liked=!card.Liked;ShowToast(action=="like"?T["Runtime53"]:T["Runtime1"]);}catch(Exception e){CommunityStatus=e.Message;}}
    [RelayCommand] private async Task ReportCreationAsync(){if(SelectedCreation==null)return;try{await community.Rpc("report_content",new{target=SelectedCreation.Id,reason="other",details="Signalement depuis MyVoice. À examiner."});ShowToast(T["Runtime98"]);}catch(Exception e){CommunityStatus=e.Message;}}
    [RelayCommand] private async Task InstallCommunityCreationAsync()
    {
        if(SelectedCreation is not {} card||CommunityBusy)return;if(InstalledEntries(card).Any()){card.State=T["Runtime48"];ShowToast(card.State);return;}CommunityBusy=true;card.State=T["Runtime109"];
        try{var path=await community.Download(card.Id,Path.Combine(store.Root,"cache","community"),new Progress<double>(value=>card.Progress=value));await LoadCommunityCover(card);await InstallCreation(path,card.Id,card);card.State=T["Runtime48"];Product.Installed.Add(new(card.Id,"",card.Kind,card.Title,DateTime.UtcNow));SaveProduct();ShowToast(T["Runtime35"]);}catch(Exception e){card.State=T["Runtime90"];CommunityStatus=e.Message;}finally{CommunityBusy=false;}
    }
    [RelayCommand] private async Task PublishPackageAsync()
    {
        if(!CanPublishDraft){PublicationStatus=PublicationAvailability;return;}
        CommunityBusy=true;
        try
        {
            var link=await community.Publish(publicationDraft.PackagePath,new{title=PublicationTitle,rights_confirmed=true,visibility=PublishVisibility,description=PublishDescription,category=PublishCategory,tags=PublishTags.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).Select(t=>t[..Math.Min(t.Length,30)]).ToArray(),language=Settings.Language});
            savedPublicationState=PublicationState();PublicationStatus=T["Runtime80"]+link;await RefreshOwnPublicationsAsync();
            try{System.Windows.Clipboard.SetText(link);ShowToast(T["Runtime81"]);}catch{ShowToast(T["Runtime82"]);}
        }
        catch(Exception e){PublicationStatus="Publication impossible : "+e.Message;}
        finally{CommunityBusy=false;}
    }
}
