using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.Infrastructure;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private PublicationDraftStore publicationDraft=null!;
    private bool initializingPublication;
    public string PublishCategory {get=>Product.PublicationCategory;set{Product.PublicationCategory=Array.IndexOf(SoundCategories,value)>=0?value:"Other";SaveProduct();OnPropertyChanged();}}
    public string PublishTags {get=>Product.PublicationTags;set{Product.PublicationTags=value;SaveProduct();OnPropertyChanged();}}
    [ObservableProperty] private string publicationStatus="Choisissez un package exporté depuis Studio pour préparer votre publication.";
    public string PublicationTitle=>string.IsNullOrEmpty(Product.PublicationTitle)?"Aucun package sélectionné":Product.PublicationTitle;
    public string PublicationKind=>Product.PublicationKind switch{"soundboard"=>"Soundboard","voice-preset"=>"Preset vocal",_=>"Package MyVoice"};
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
    private void NotifyPublication(){OnPropertyChanged(nameof(CanPublishDraft));OnPropertyChanged(nameof(PublicationAvailability));OnPropertyChanged(nameof(HasPublicationPackage));OnPropertyChanged(nameof(PublicationTitle));OnPropertyChanged(nameof(PublicationKind));}
    partial void OnPublishDescriptionChanged(string value){Product.PublicationDescription=value;if(!initializingPublication)SaveProduct();}
    partial void OnPublishVisibilityChanged(string value){if(Array.IndexOf(Visibilities,value)<0)return;Product.PublicationVisibility=value;if(!initializingPublication)SaveProduct();}
    partial void OnPublishRightsChanged(bool value)=>NotifyPublication();
    partial void OnCommunityBusyChanged(bool value)=>NotifyPublication();
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
            var manifest=await publicationDraft.PrepareAsync(file);
            Product.PublicationTitle=manifest.Title;Product.PublicationKind=manifest.Kind;PublishRights=false;SaveProduct();
            PublicationStatus="Package vérifié et copié dans votre brouillon local. "+(manifest.Kind=="soundboard"?manifest.Sounds.Count+" sons.":"Preset vocal.");
        }
        catch(Exception e){PublicationStatus="Préparation impossible : "+e.Message;}
        finally{CommunityBusy=false;NotifyPublication();}
    }
}
