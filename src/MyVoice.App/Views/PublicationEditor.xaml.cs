using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using MyVoice.App.ViewModels;

namespace MyVoice.App.Views;
public partial class PublicationEditor:Window
{
    private readonly MainViewModel model;
    private readonly string id;
    private string? cover,preview;
    private bool busy,saved;
    private string originalState="";
    private string CurrentState()=>string.Join("\u001f",CreationTitle.Text,Description.Text,Tags.Text,Category.SelectedItem,VisibilityChoice.SelectedValue,cover,preview);
    private readonly string language;
    public PublicationEditor(MainViewModel model,CommunityCard card,JsonElement data)
    {
        InitializeComponent();this.model=model;id=card.Id;DataContext=model;Owner=Application.Current.MainWindow;Icon=Owner?.Icon;
        CreationTitle.Text=data.GetProperty("title").GetString();Description.Text=data.GetProperty("description").GetString();Tags.Text=string.Join(", ",data.GetProperty("tags").EnumerateArray().Select(t=>t.GetString()));
        VisibilityChoice.SelectedValue=data.GetProperty("visibility").GetString();Category.SelectedItem=data.GetProperty("category").GetString();language=data.GetProperty("language").GetString()??"und";
        bool locked=data.GetProperty("audio_locked").GetBoolean();ReplaceAudioButton.IsEnabled=!locked;
        AudioNotice.Text=model.T[locked?"PublicationAudioLocked":"PublicationAudioPrivate"];CoverPreview.Circular=card.IsVoice;CoverPreview.FilePath=card.Cover;
        originalState=CurrentState();ContentRendered+=(_,_)=>originalState=CurrentState();
        Closing+=(_,e)=>{if(busy){e.Cancel=true;return;}if(!saved&&CurrentState()!=originalState&&!Services.Dialogs.Confirm(model.T["PublicationDiscardChanges"]))e.Cancel=true;};
    }
    private void Cancel(object sender,RoutedEventArgs e)=>Close();
    private void ChooseCover(object sender,RoutedEventArgs e){var file=model.ChoosePublicationEditCover(CoverPreview.Circular);if(file!=null){cover=file;CoverPreview.FilePath=file;}}
    private void ChoosePreview(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Filter="Audio |*.wav;*.mp3;*.flac;*.ogg;*.m4a"};if(dialog.ShowDialog(this)==true){preview=dialog.FileName;Status.Text=Path.GetFileName(preview);}
    }
    private async void ChoosePackage(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Filter="MyVoice |*.myvoice"};if(dialog.ShowDialog(this)!=true)return;
        if(!Services.Dialogs.Confirm(model.T["PublicationReplaceConfirm"]))return;
        busy=true;Fields.IsEnabled=false;SaveButton.IsEnabled=false;
        try{await model.ReplacePublicationAudio(id,dialog.FileName);Status.Text=model.T["PublicationAudioReplaced"];}
        catch(Exception ex){Status.Text=ex.Message;}
        finally{busy=false;Fields.IsEnabled=true;SaveButton.IsEnabled=true;}
    }
    private async void Save(object sender,RoutedEventArgs e)
    {
        if(busy)return;busy=true;Fields.IsEnabled=false;SaveButton.IsEnabled=false;Status.Text=model.T["PublicationEditSaving"];
        try
        {
            await model.SavePublicationEdit(id,CreationTitle.Text,Description.Text,Tags.Text,Category.SelectedItem as string??"Other",language,VisibilityChoice.SelectedValue as string??"private",cover,preview);
            busy=false;saved=true;DialogResult=true;
        }
        catch(Exception ex){Status.Text=ex.Message;}
        finally{busy=false;Fields.IsEnabled=true;SaveButton.IsEnabled=true;}
    }
}
