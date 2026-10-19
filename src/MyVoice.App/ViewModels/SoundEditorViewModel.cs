using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Core;
using MyVoice.Infrastructure;
using MyVoice.Soundboard;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private SoundCard? editingSound;
    [ObservableProperty] private string editorName="";
    [ObservableProperty] private string editorDescription="";
    [ObservableProperty] private string editorTags="";
    [ObservableProperty] private string editorCategory="Other";
    [ObservableProperty] private double editorStart;
    [ObservableProperty] private double editorEnd;
    [ObservableProperty] private double editorFadeIn;
    [ObservableProperty] private double editorFadeOut;
    [ObservableProperty] private bool editorNormalize;
    [ObservableProperty] private bool editorBusy;
    [ObservableProperty] private string editorStatus="Choisissez un son puis « Modifier dans Studio » dans son menu.";
    public string[] SoundCategories {get;}=["Meme","SFX","Gaming","Anime","Reaction","Music","Voice","Ambient","Movie/TV","Other"];
    public bool HasEditableSound=>editingSound?.Owned==true;
    public void EditSoundInStudio(SoundCard card)
    {
        if(!card.Owned){ShowToast(T["Runtime15"]);return;}
        try
        {
            using var reader=new NAudio.Wave.AudioFileReader(card.Item.File);editingSound=card;SelectedSound=card;
            EditorName=card.Name;EditorDescription=card.Item.Description;EditorCategory=card.Item.Category;EditorTags=string.Join(", ",card.Item.Tags);
            EditorStart=0;EditorEnd=reader.TotalTime.TotalSeconds;EditorFadeIn=0;EditorFadeOut=0;EditorNormalize=false;
            EditorStatus=T["Runtime52"];OnPropertyChanged(nameof(HasEditableSound));Page=2;StudioTab=3;
        }
        catch(Exception e){StudioError(e.Message);}
    }
    [RelayCommand] private async Task ApplySoundEditAsync()
    {
        if(editingSound is not {Owned:true} card||EditorBusy)return;
        if(string.IsNullOrWhiteSpace(EditorName)){EditorStatus="Donnez un nom au son.";return;}
        EditorBusy=true;
        try
        {
            var destination=Path.Combine(store.Root,"sounds",Guid.NewGuid()+".wav");
            await SoundEditor.RenderAsync(card.Item.File,destination,new(EditorStart,EditorEnd,EditorFadeIn,EditorFadeOut,EditorNormalize),lifetime.Token);
            sounds.Stop(card.Item.Id);card.Item.File=destination;card.Item.Name=EditorName.Trim();card.Item.Description=EditorDescription;
            card.Item.Category=SoundCategories.Contains(EditorCategory)?EditorCategory:"Other";
            card.Item.Tags=EditorTags.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(t=>t[..Math.Min(t.Length,32)]).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
            sounds.Save();card.Refresh();SoundView.Refresh();EditorStart=0;using var reader=new NAudio.Wave.AudioFileReader(destination);EditorEnd=reader.TotalTime.TotalSeconds;
            EditorStatus=T["Runtime60"];
        }
        catch(Exception e){EditorStatus=e.Message;}
        finally{EditorBusy=false;}
    }
    [RelayCommand] private async Task PublishSelectedSoundAsync(){if(SelectedSound is {} card)await PrepareSoundPublication(card);}
    public async Task PrepareSoundPublication(SoundCard card)
    {
        if(!card.Owned){ShowToast(T["Runtime111"]);return;}
        var file=Path.Combine(store.Root,"temp",Guid.NewGuid()+".myvoice");Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try{await Task.Run(()=>CreationPackage.ExportBoard(file,new SoundFolder{Name=card.Name,Cover=card.Cover},new[]{card.Item}));await PreparePublicationAsync(file);PublishDescription=card.Item.Description;PublishCategory=card.Item.Category;PublishTags=string.Join(", ",card.Item.Tags);ShowPublication=true;}
        catch(Exception e){StudioError(e.Message);}finally{if(File.Exists(file))File.Delete(file);}
    }
}
