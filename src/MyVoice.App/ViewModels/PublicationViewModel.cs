using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.Infrastructure;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    public async Task PreparePresetPublication(MyVoice.Core.DesignedVoice preset)
    {
        if(!string.IsNullOrEmpty(preset.SourceContentId)){ShowToast("Une création importée ne peut pas être republiée.");return;}
        var folder=Path.Combine(store.Root,"cache","publication");Directory.CreateDirectory(folder);
        var file=Path.Combine(folder,Guid.NewGuid()+".myvoice");
        try{await Task.Run(()=>CreationPackage.ExportPreset(file,preset));await PreparePublicationAsync(file);ShowPublication=true;}
        catch(Exception e){ShowToast("Préparation impossible : "+e.Message);}
        finally{if(File.Exists(file))File.Delete(file);}
    }
    public void OpenVoiceInspector(object voice){
        var panel=new System.Windows.Controls.StackPanel{Margin=new System.Windows.Thickness(24)};
        var name=voice is MyVoice.Core.VoiceReference a?a.Name:((MyVoice.Core.DesignedVoice)voice).Name;
        panel.Children.Add(new System.Windows.Controls.TextBlock{Text=name,FontSize=24});
        panel.Children.Add(new System.Windows.Controls.TextBlock{Text=voice is MyVoice.Core.VoiceReference ai?ai.Summary:"Voice Preset",Margin=new System.Windows.Thickness(0,12,0,12)});
        var window=new System.Windows.Window{Title="Inspector",Owner=System.Windows.Application.Current.MainWindow,Icon=System.Windows.Application.Current.MainWindow?.Icon,Width=520,Height=300,Content=panel,WindowStartupLocation=System.Windows.WindowStartupLocation.CenterOwner};window.SetResourceReference(System.Windows.Window.BackgroundProperty,"SurfaceBrush");window.SetResourceReference(System.Windows.Window.ForegroundProperty,"TextBrush");
        if(voice is MyVoice.Core.VoiceReference v){var preview=new System.Windows.Controls.Button{Content="Preview · casque"};preview.Click+=(_,_)=>PreviewLocalFile(v.ReferenceFile);panel.Children.Add(preview);}
        bool owned=voice is MyVoice.Core.VoiceReference av?string.IsNullOrEmpty(av.SourceContentId):string.IsNullOrEmpty(((MyVoice.Core.DesignedVoice)voice).SourceContentId);
        if(owned){var publish=new System.Windows.Controls.Button{Content="Publish & Share"};publish.Click+=async(_,_)=>{window.Close();if(voice is MyVoice.Core.VoiceReference av2)await PrepareAiPublication(av2);else await PreparePresetPublication((MyVoice.Core.DesignedVoice)voice);};panel.Children.Add(publish);}window.ShowDialog();
    }
    [ObservableProperty] private bool showPublication;
    partial void OnShowPublicationChanged(bool value){if(value&&SignedIn)_=RefreshOwnPublicationsAsync();}
    [RelayCommand] private void OpenPublication()=>ShowPublication=true;
    [RelayCommand] private void ClosePublication(){SavePublicationDraft();ShowPublication=false;}
    [RelayCommand] private async Task PublishSelectedAiAsync(){if(SelectedAiVoice is {} voice)await PrepareAiPublication(voice);}
    public async Task PrepareAiPublication(MyVoice.Core.VoiceReference voice){
        if(!string.IsNullOrEmpty(voice.SourceContentId)){ShowToast("Une voix téléchargée ne peut pas être republiée.");return;}
        var folder=Path.Combine(store.Root,"cache","publication");Directory.CreateDirectory(folder);var file=Path.Combine(folder,Guid.NewGuid()+".myvoice");
        try{await Task.Run(()=>CreationPackage.ExportAiVoice(file,voice));await PreparePublicationAsync(file);ShowPublication=true;}catch(Exception e){ShowToast(e.Message);}finally{if(File.Exists(file))File.Delete(file);}
    }
    [RelayCommand] private async Task PreviewPublicationAsync(){if(!HasPublicationPackage)return;try{var p=await Task.Run(()=>CreationPackage.Extract(publicationDraft.PackagePath,Path.Combine(store.Root,"cache","draft-preview")));var audio=p.Manifest.AiVoice?.ReferenceFile??p.Manifest.Sounds.FirstOrDefault()?.File;if(audio==null){ShowToast("Ce preset ne contient pas de référence audio.");return;}PreviewLocalFile(Path.Combine(p.Directory,audio));}catch(Exception e){PublicationStatus=e.Message;}}
    private PublicationDraftStore publicationDraft=null!;
    private bool initializingPublication;
    public string PublishCategory {get=>Product.PublicationCategory;set{Product.PublicationCategory=Array.IndexOf(SoundCategories,value)>=0?value:"Other";SaveProduct();OnPropertyChanged();}}
    public string PublishTags {get=>Product.PublicationTags;set{Product.PublicationTags=value;SaveProduct();OnPropertyChanged();}}
    [ObservableProperty] private string publicationStatus="Choisissez un package exporté depuis Studio pour préparer votre publication.";
    public string PublicationTitle {get=>Product.PublicationTitle;set{Product.PublicationTitle=value;SaveProduct();OnPropertyChanged();}}
    public string PublicationContents=>Product.PublicationKind=="ai-voice"?"Cette publication partage la référence vocale et les extraits utilisables sélectionnés. Les originaux exclus et les chemins privés restent sur ce PC.":"Cette publication partage uniquement les sons ou le preset du package sélectionné. Rien n’est envoyé avant Publier.";
    public string? PublicationCover=>Product.PublicationCover;
    public string PublicationKind=>Product.PublicationKind switch{"soundboard"=>"Soundboard","voice-preset"=>"Preset vocal","ai-voice"=>"AI Voice",_=>"Package MyVoice"};
    public bool HasPublicationPackage=>publicationDraft!=null&&File.Exists(publicationDraft.PackagePath);
    public bool CanPublishDraft=>CommunityConfigured&&SignedIn&&HasPublicationPackage&&PublishRights&&!CommunityBusy;
    public string PublicationAvailability=>!CommunityConfigured?"Publication en ligne indisponible : le service MyVoice n’est pas encore connecté. Vous pouvez préparer et conserver votre brouillon ici.":!SignedIn?"Connectez-vous avec un compte dont l’email est confirmé pour publier.":"Le bouton Publier envoie uniquement le package sélectionné et les informations de ce brouillon.";
    private void InitializePublication()
    {
        publicationDraft=new(store.Root);initializingPublication=true;PublishDescription=Product.PublicationDescription;
        PublishVisibility=Array.IndexOf(Visibilities,Product.PublicationVisibility)>=0?Product.PublicationVisibility:"private";
        initializingPublication=false;
        if(HasPublicationPackage)PublicationStatus="Brouillon conservé sur ce PC. Aucun envoi automatique.";
    }
    private void NotifyPublication(){OnPropertyChanged(nameof(CanPublishDraft));OnPropertyChanged(nameof(PublicationAvailability));OnPropertyChanged(nameof(HasPublicationPackage));OnPropertyChanged(nameof(PublicationTitle));OnPropertyChanged(nameof(PublicationKind));OnPropertyChanged(nameof(PublicationContents));}
    partial void OnPublishDescriptionChanged(string value){Product.PublicationDescription=value;if(!initializingPublication)SaveProduct();}
    partial void OnPublishVisibilityChanged(string value){if(Array.IndexOf(Visibilities,value)<0)return;Product.PublicationVisibility=value;if(!initializingPublication)SaveProduct();}
    partial void OnPublishRightsChanged(bool value)=>NotifyPublication();
    partial void OnCommunityBusyChanged(bool value)=>NotifyPublication();
    [RelayCommand] private async Task RestorePublicationDraftAsync(){var history=Path.Combine(store.Root,"drafts","history");Directory.CreateDirectory(history);var dialog=new OpenFileDialog{Title="Brouillons conservés",InitialDirectory=history,Filter="Brouillon MyVoice|*.myvoice"};if(dialog.ShowDialog()==true)await PreparePublicationAsync(dialog.FileName);}
    [RelayCommand] private void SavePublicationDraft(){SaveProduct();PublicationStatus="Brouillon enregistré sur ce PC. Rien n’a été publié.";}
    [RelayCommand] private async Task ChoosePublicationPackageAsync()
    {
        if(CommunityBusy)return;
        var dialog=new OpenFileDialog{Title="Préparer une publication",Filter="Package MyVoice exporté|*.myvoice;*.zip"};
        if(dialog.ShowDialog()!=true)return;
        await PreparePublicationAsync(dialog.FileName);
    }
    public async Task PreparePublicationAsync(string file)
    {
        CommunityBusy=true;
        try
        {
            PublicationDraftMetadata? restored=null;
            if(File.Exists(file+".json"))restored=System.Text.Json.JsonSerializer.Deserialize<PublicationDraftMetadata>(await File.ReadAllTextAsync(file+".json"));
            var manifest=await publicationDraft.PrepareAsync(file,new(PublicationTitle,PublishDescription,PublishVisibility,PublishCategory,PublishTags));
            Product.PublicationTitle=manifest.Title;Product.PublicationKind=manifest.Kind;Product.PublicationCover=publicationDraft.PreparedCoverPath;OnPropertyChanged(nameof(PublicationCover));PublishRights=false;SaveProduct();
            if(restored!=null){PublicationTitle=restored.Title;PublishDescription=restored.Description;PublishVisibility=Array.IndexOf(Visibilities,restored.Visibility)>=0?restored.Visibility:"private";PublishCategory=restored.Category;PublishTags=restored.Tags;}
            PublicationStatus="Package vérifié et copié dans votre brouillon local. "+(manifest.Kind=="soundboard"?manifest.Sounds.Count+" sons.":manifest.Kind=="ai-voice"?"Référence IA et extraits utilisables.":"Preset vocal.");
        }
        catch(Exception e){PublicationStatus="Préparation impossible : "+e.Message;}
        finally{CommunityBusy=false;NotifyPublication();}
    }
}
