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
    [ObservableProperty] private double progress;
    [ObservableProperty] private string state="Installer";
}
public sealed record SocialContact(string Id,string Name,string Username,string Kind);
public sealed record CreatorProfile(string Id,string Username,string DisplayName,string Bio,bool Verified,string Relationship);
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
    public string[] ContentSorts {get;}=["Recent","Featured"];
    public string[] Visibilities {get;}=["private","unlisted","friends","public"];
    public ObservableCollection<CommunityCard> CommunityItems {get;}=[];
    public ObservableCollection<SocialContact> SocialContacts {get;}=[];
    [ObservableProperty] private string socialCounts="";
    [RelayCommand] private async Task RefreshSocialAsync(){try{var data=await community.Rpc("social_overview",new{});SocialContacts.Clear();foreach(var type in new[]{"friends","requests"})foreach(var p in data.GetProperty(type).EnumerateArray())SocialContacts.Add(new(p.GetProperty("id").GetString()!,p.GetProperty("name").GetString()!,p.GetProperty("username").GetString()!,p.GetProperty("kind").GetString()!));SocialCounts=$"{data.GetProperty("followers")} abonnés · {data.GetProperty("following")} suivis";}catch(Exception e){AccountStatus=e.Message;}}
    [RelayCommand] private async Task AcceptFollowAsync(SocialContact? contact)=>await AnswerFollow(contact,true);
    [RelayCommand] private async Task DeclineFollowAsync(SocialContact? contact)=>await AnswerFollow(contact,false);
    private async Task AnswerFollow(SocialContact? contact,bool accept){if(contact==null||contact.Kind!="request")return;try{await community.Rpc("answer_follow",new{requester=contact.Id,accept});await RefreshSocialAsync();ShowToast(accept?"Demande acceptée.":"Demande refusée.");}catch(Exception e){AccountStatus=e.Message;}}

    private void InitializeCommunity()
    {
        community=new(CommunityConfiguration.Load(Path.Combine(AppContext.BaseDirectory,"community.public.json")),store.Root);
        if(CommunityConfigured){CommunityStatus="Prêt à explorer les créations.";AccountStatus=SignedIn?"Session enregistrée de façon sécurisée sur ce PC.":"Connectez-vous ou créez un compte.";}
    }
    public async Task AccountAction(string action,string email,string username,string display,string password,string confirmation)
    {
        if(CommunityBusy)return;CommunityBusy=true;
        try
        {
            if(action=="register"){await community.Register(email,username,display,password,confirmation);AccountStatus="Vérifiez votre boîte email pour confirmer votre compte.";}
            else if(action=="recover"){await community.Recover(email);AccountStatus="Si ce compte existe, un email de récupération a été envoyé.";}
            else {await community.Login(email,password);var own=(await community.OwnProfile()).EnumerateArray().FirstOrDefault();if(own.ValueKind==JsonValueKind.Object){ProfilePrivate=own.GetProperty("private").GetBoolean();ProfileName=own.GetProperty("display_name").GetString()!;ProfileBio=own.GetProperty("bio").GetString()!;}AccountStatus="Connecté. Vos créations locales restent privées.";}
            OnPropertyChanged(nameof(SignedIn));
        }catch(Exception e){AccountStatus=e.Message;}finally{CommunityBusy=false;}
    }
    [RelayCommand] private async Task LogoutAsync(){try{await community.Logout();}catch{}finally{OnPropertyChanged(nameof(SignedIn));AccountStatus="Déconnecté. Votre bibliothèque locale reste disponible.";}}
    [RelayCommand] private async Task SaveOnlineProfileAsync(){try{await community.UpdateProfile(ProfileName,ProfileBio,ProfilePrivate);ShowToast("Profil mis à jour.");}catch(Exception e){AccountStatus=e.Message;}}
    [RelayCommand] private async Task RefreshCommunityAsync()
    {
        if(CommunityBusy)return;CommunityBusy=true;CommunityStatus="Recherche des créations…";
        try{var feed=await community.Feed(CommunitySearch,CommunityKind,CommunitySort);CommunityItems.Clear();foreach(var item in feed.EnumerateArray())CommunityItems.Add(new(){Id=item.GetProperty("id").GetString()!,Title=item.GetProperty("title").GetString()!,Description=item.GetProperty("description").GetString()!,Kind=item.GetProperty("kind").GetString()!,Owner=item.GetProperty("owner_id").GetString()!});CommunityStatus=CommunityItems.Count==0?"Aucune création pour cette recherche.":CommunityItems.Count+" créations disponibles.";}catch(Exception e){CommunityStatus=e.Message;}finally{CommunityBusy=false;}
    }
    [RelayCommand] private async Task FindCreatorAsync()
    {
        try{var p=await community.Rpc("creator_by_username",new{handle=CommunitySearch.TrimStart('@')});SelectedCreator=p.ValueKind==JsonValueKind.Null?null:new(p.GetProperty("id").GetString()!,p.GetProperty("username").GetString()!,p.GetProperty("display_name").GetString()!,p.GetProperty("bio").GetString()!,p.GetProperty("verified").GetBoolean(),p.GetProperty("relationship").GetString()!);CommunityStatus=SelectedCreator==null?"Créateur introuvable.":"Profil chargé.";}catch(Exception e){CommunityStatus=e.Message;}
    }
    [RelayCommand] private async Task FollowCreatorAsync(){if(SelectedCreator==null)return;try{var result=await community.Rpc("follow_creator",new{target=SelectedCreator.Id,enabled=SelectedCreator.Relationship is not("Friends" or "Following")});SelectedCreator=SelectedCreator with{Relationship=result.GetString()!};ShowToast(SelectedCreator.Relationship=="Friends"?"Vous êtes maintenant amis.":SelectedCreator.Relationship);}catch(Exception e){CommunityStatus=e.Message;}}
    [RelayCommand] private async Task BlockCreatorAsync(){if(SelectedCreator==null)return;try{await community.Rpc("block_creator",new{target=SelectedCreator.Id,enabled=true});SelectedCreator=null;ShowToast("Utilisateur bloqué.");}catch(Exception e){CommunityStatus=e.Message;}}
    [RelayCommand] private async Task LikeCreationAsync()=>await React("like");
    [RelayCommand] private async Task SaveCreationAsync()=>await React("save");
    private async Task React(string action){if(SelectedCreation==null)return;try{await community.Rpc("react_to_content",new{target=SelectedCreation.Id,action,enabled=true});ShowToast(action=="like"?"Like enregistré.":"Ajouté aux favoris du compte.");}catch(Exception e){CommunityStatus=e.Message;}}
    [RelayCommand] private async Task ReportCreationAsync(){if(SelectedCreation==null)return;try{await community.Rpc("report_content",new{target=SelectedCreation.Id,reason="other",details="Signalement depuis MyVoice. À examiner."});ShowToast("Signalement envoyé.");}catch(Exception e){CommunityStatus=e.Message;}}
    [RelayCommand] private async Task InstallCommunityCreationAsync()
    {
        if(SelectedCreation is not {} card||CommunityBusy)return;CommunityBusy=true;card.State="Téléchargement…";
        try{var path=await community.Download(card.Id,Path.Combine(store.Root,"cache","community"),new Progress<double>(value=>card.Progress=value));await InstallCreation(path);card.State="Installé ✓";Product.Installed.Add(new(card.Id,"",card.Kind,card.Title,DateTime.UtcNow));SaveProduct();ShowToast("Création installée, disponible hors ligne.");}catch(Exception e){card.State="Réessayer";CommunityStatus=e.Message;}finally{CommunityBusy=false;}
    }
    [RelayCommand] private async Task PublishPackageAsync()
    {
        if(!PublishRights){ShowToast("Confirmez que vous avez les droits de partager ces fichiers.");return;}
        if(!SignedIn){AccountStatus="Connectez-vous et vérifiez votre email pour publier.";Page=7;return;}
        var dialog=new OpenFileDialog{Filter="Package MyVoice exporté|*.myvoice;*.zip"};if(dialog.ShowDialog()!=true)return;
        CommunityBusy=true;try{var link=await community.Publish(dialog.FileName,new{rights_confirmed=true,visibility=PublishVisibility,description=PublishDescription,tags=Array.Empty<string>(),language=Settings.Language});System.Windows.Clipboard.SetText(link);ShowToast("Publié. Le lien a été copié.");}catch(Exception e){CommunityStatus=e.Message;}finally{CommunityBusy=false;}
    }
}
