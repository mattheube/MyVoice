using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    [ObservableProperty] private string profileUsername="";
    [ObservableProperty] private string profileEmail="";
    [ObservableProperty] private string profileMetrics="Chargement des statistiques…";
    [ObservableProperty] private bool profileVerified;
    [ObservableProperty] private long profileFollowerCount;
    [ObservableProperty] private long profileFollowingCount;
    [ObservableProperty] private long profileLikeCount;
    [ObservableProperty] private string profilePublicationKind="all";
    public ObservableCollection<CommunityCard> OwnPublications {get;}=[];
    public System.Collections.Generic.IEnumerable<CommunityCard> ProfilePublications=>OwnPublications.Where(c=>ProfilePublicationKind=="all"||c.Kind==ProfilePublicationKind);
    partial void OnProfilePublicationKindChanged(string value)=>OnPropertyChanged(nameof(ProfilePublications));
    internal static CommunityCard ReadCommunityCard(JsonElement c)=>new(){Id=c.GetProperty("id").GetString()!,Title=c.GetProperty("title").GetString()!,Description=c.GetProperty("description").GetString()!,Kind=c.GetProperty("kind").GetString()!,Owner=c.GetProperty("owner_id").GetString()!,Username=c.GetProperty("username").GetString()!,Official=c.GetProperty("official").GetBoolean(),Liked=c.GetProperty("liked").GetBoolean(),Visibility=c.GetProperty("visibility").GetString()!,Tags=c.GetProperty("tags").EnumerateArray().Select(t=>t.GetString()!).ToArray()};
    [RelayCommand] private async Task RefreshOwnPublicationsAsync(){if(!SignedIn)return;try{var data=await community.Publications(true);OwnPublications.Clear();foreach(var c in data.EnumerateArray())OwnPublications.Add(ReadCommunityCard(c));OnPropertyChanged(nameof(ProfilePublications));await LoadCommunityCovers(OwnPublications.ToArray());}catch(Exception e){AccountStatus=e.Message;}}
    [RelayCommand] private async Task RefreshPublicProfileAsync(){
        if(!SignedIn)return;
        try{
            var p=(await community.OwnProfile()).EnumerateArray().First();ProfileUsername=p.GetProperty("username").GetString()!;ProfileName=p.GetProperty("display_name").GetString()!;ProfileBio=p.GetProperty("bio").GetString()!;ProfilePrivate=p.GetProperty("private").GetBoolean();ProfileVerified=p.GetProperty("verified").GetBoolean();
            var avatar=p.GetProperty("avatar").GetString();if(avatar?.StartsWith("data:image/jpeg;base64,")==true){var path=Path.Combine(store.Root,"cache","profile-avatar.jpg");Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path,Convert.FromBase64String(avatar.Split(',')[1]));Product.Avatar=path;OnPropertyChanged(nameof(ProfileAvatar));}
            ProfileEmail=(await community.User()).GetProperty("email").GetString()??"";
            var totals=await community.Rpc("profile_totals",new{target=community.Session!.UserId});ProfileMetrics=$"{totals.GetProperty("followers")} abonnés · {totals.GetProperty("following")} abonnements · {totals.GetProperty("likes")} likes reçus";ProfileFollowerCount=totals.GetProperty("followers").GetInt64();ProfileFollowingCount=totals.GetProperty("following").GetInt64();ProfileLikeCount=totals.GetProperty("likes").GetInt64();
            await RefreshOwnPublicationsAsync();await RefreshSocialAsync();
        }catch(Exception e){AccountStatus=e.Message;}
    }
    [RelayCommand] private async Task SaveProfilePrivacyAsync(){try{await community.UpdatePrivacy(ProfilePrivate);AccountStatus=T["Runtime21"];}catch(Exception e){AccountStatus=e.Message;}}
    [RelayCommand] private async Task DeleteAccountAsync(){if(!SignedIn||MyVoice.App.Services.Dialogs.Prompt(T["Runtime105"])!="SUPPRIMER")return;try{await community.DeleteAccount();await LogoutAsync();AccountStatus=T["Runtime20"];}catch(Exception e){AccountStatus=e.Message;}}
    private Task<string?> ProfileAvatarData(){
        if(ProfileAvatar==null||!File.Exists(ProfileAvatar))return Task.FromResult<string?>(null);
        using var source=File.OpenRead(ProfileAvatar);var decoder=BitmapDecoder.Create(source,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);var frame=decoder.Frames[0];var resized=new TransformedBitmap(frame,new System.Windows.Media.ScaleTransform(128d/frame.PixelWidth,128d/frame.PixelHeight));var encoder=new JpegBitmapEncoder{QualityLevel=80};encoder.Frames.Add(BitmapFrame.Create(resized));using var output=new MemoryStream();encoder.Save(output);return Task.FromResult<string?>("data:image/jpeg;base64,"+Convert.ToBase64String(output.ToArray()));
    }
    public async Task ChangeAccountPassword(string password,string confirmation){try{await community.ChangePassword(password,confirmation);AccountStatus=T["Runtime61"];}catch(Exception e){AccountStatus=e.Message;}}
    private readonly System.Threading.SemaphoreSlim communityCoverGate=new(2,2);
    private async Task LoadCommunityCovers(CommunityCard[] cards){await Task.WhenAll(cards.Select(async card=>{await communityCoverGate.WaitAsync();try{if(!lifetime.IsCancellationRequested)card.Cover=await community.DownloadCover(card.Id,Path.Combine(store.Root,"cache","community-covers"));}catch{/* Older publications may not have a cover. */}finally{communityCoverGate.Release();}}));}
    private MyVoice.Core.VoiceReference? communityTrialVoice;
    public async Task PreviewCommunity(CommunityCard card)
    {
        SelectedCreation=card;if(CommunityBusy)return;CommunityBusy=true;
        try
        {
            var file=await community.Download(card.Id,Path.Combine(store.Root,"cache","community"));
            var result=await Task.Run(()=>MyVoice.Infrastructure.CreationPackage.Extract(file,Path.Combine(store.Root,"cache","community-preview")));
            File.Delete(file);var manifest=result.Manifest;
            card.Cover=manifest.Cover==null?null:Path.Combine(result.Directory,manifest.Cover);
            if(manifest.Kind=="ai-voice" && manifest.AiVoice is {} voice)
            {
                voice.Id="trial-"+card.Id;voice.Name=card.Title;
                voice.ReferenceFile=Path.Combine(result.Directory,voice.ReferenceFile);voice.Cover=card.Cover;
                voice.PreparedReferenceFiles=voice.PreparedReferenceFiles?.Select(f=>Path.Combine(result.Directory,f)).ToList();
                foreach(var sample in voice.Samples){sample.OriginalFile=Path.Combine(result.Directory,sample.OriginalFile);sample.Segments=sample.Segments.Select(segment=>segment with{File=Path.Combine(result.Directory,segment.File)}).ToList();}
                communityTrialVoice=voice;await UseAiVoiceAsync(voice);
                CommunityStatus=AiLive?T["CommunityVoiceTrial"]:Status;
            }
            else if(manifest.Kind=="voice-preset" && manifest.Preset is {} preset)
            {
                var previous=Settings.LastDesignedVoiceId;UseDesignedVoice(preset);Settings.LastDesignedVoiceId=previous;Save();
                CommunityStatus=T["CommunityVoiceTrial"];
            }
            else if(manifest.Sounds.FirstOrDefault() is {} sound)PreviewLocalFile(Path.Combine(result.Directory,sound.File));
            else CommunityStatus=T["Runtime13"];
        }
        catch(Exception e){CommunityStatus=e.Message;}
        finally{CommunityBusy=false;}
    }
    public void InspectCommunity(CommunityCard card){SelectedCreation=card;var panel=new System.Windows.Controls.StackPanel{Margin=new System.Windows.Thickness(24)};panel.Children.Add(new System.Windows.Controls.TextBlock{Text=card.Title,FontSize=24});panel.Children.Add(new System.Windows.Controls.TextBlock{Text=card.CreatorLabel+"\n"+card.Description+"\n"+string.Join(" · ",card.Tags),TextWrapping=System.Windows.TextWrapping.Wrap});var preview=new System.Windows.Controls.Button{Content="Preview · casque"};preview.Click+=async(_,_)=>await PreviewCommunity(card);panel.Children.Add(preview);var window=new System.Windows.Window{Title="Inspector",Owner=System.Windows.Application.Current.MainWindow,Icon=System.Windows.Application.Current.MainWindow?.Icon,Width=550,Height=360,Content=panel,WindowStartupLocation=System.Windows.WindowStartupLocation.CenterOwner};window.SetResourceReference(System.Windows.Window.BackgroundProperty,"SurfaceBrush");window.SetResourceReference(System.Windows.Window.ForegroundProperty,"TextBrush");window.ShowDialog();}
    public async Task ViewCommunityCreator(CommunityCard card){CommunitySearch=card.Username;await FindCreatorAsync();}
    public async Task AddCommunityCollection(CommunityCard card,MyVoice.Core.LibraryCollection? target=null){
        var entries=InstalledEntries(card).ToArray();
        if(entries.Length==0){SelectedCreation=card;await InstallCommunityCreationAsync();if(card.State!=T["Runtime48"])return;entries=InstalledEntries(card).ToArray();}
        if(entries.Length==0)return;
        if(target==null){var name=MyVoice.App.Services.Dialogs.Prompt("Nouvelle collection",card.Title);if(name==null)return;target=new MyVoice.Core.LibraryCollection{Name=name};Product.Collections.Add(target);Collections.Add(target);}
        foreach(var entry in entries)if(!target.Items.Contains(entry))target.Items.Add(entry);SaveProduct();SelectedCollection=target;ShowToast(T["Runtime33"]);
    }
    private System.Collections.Generic.IEnumerable<MyVoice.Core.LibraryEntry> InstalledEntries(CommunityCard card){
        foreach(var s in SoundCards.Where(s=>s.Item.SourceContentId==card.Id))yield return new("sound",s.Item.Id.ToString());
        foreach(var v in AiVoices.Where(v=>v.SourceContentId==card.Id))yield return new("ai-voice",v.Id);
        foreach(var v in DesignedVoices.Where(v=>v.SourceContentId==card.Id))yield return new("voice-preset",v.Id);
    }
}
