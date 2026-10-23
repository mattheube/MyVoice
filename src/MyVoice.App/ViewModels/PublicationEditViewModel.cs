using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.Infrastructure;
using NAudio.Wave;

namespace MyVoice.App.ViewModels;
public sealed record VisibilityOption(string Value,string Label);
public partial class MainViewModel
{
    public VisibilityOption[] VisibilityChoices=>[new("private",T["Ui583"]),new("unlisted",T["Ui599"]),new("friends",T["Ui554"]),new("public","Public")];
    public bool OwnsCreation(CommunityCard card)=>SignedIn&&community.Session!.UserId==card.Owner;
    public string? ChoosePublicationEditCover(bool circular)=>ImportCover(circular);
    public async Task EditPublication(CommunityCard card)
    {
        if(!OwnsCreation(card)||CommunityBusy)return;
        try
        {
            var data=await community.Rpc("content_by_id",new{target=card.Id});
            var dialog=new Views.PublicationEditor(this,card,data);
            if(dialog.ShowDialog()==true){await RefreshOwnPublicationsAsync();if(Page==5)await RefreshCommunityAsync();}
        }
        catch(Exception e){ShowToast(e.Message);}
    }
    public async Task ReplacePublicationAudio(string id,string file)
    {
        var manifest=CreationPackage.Inspect(file);
        await community.Publish(file,new{edit_id=id,title=manifest.Title,rights_confirmed=true,visibility="private",description="",category="Other",tags=Array.Empty<string>(),language=Settings.Language});
    }
    public async Task SavePublicationEdit(string id,string title,string description,string tags,string category,string language,string visibility,string? cover,string? preview)
    {
        string? image=null,audio=null;
        if(cover!=null)
        {
            using var source=File.OpenRead(cover);var frame=BitmapDecoder.Create(source,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
            double scale=Math.Min(1,512d/Math.Max(frame.PixelWidth,frame.PixelHeight));
            var encoder=new JpegBitmapEncoder{QualityLevel=88};encoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(frame,new ScaleTransform(scale,scale))));
            using var output=new MemoryStream();encoder.Save(output);image=Convert.ToBase64String(output.ToArray());
        }
        if(preview!=null)
        {
            audio=await Task.Run(()=>
            {
                using var reader=new AudioFileReader(preview);
                if(reader.TotalTime.TotalSeconds is <=0 or >30||reader.WaveFormat.SampleRate is <16000 or >48000||reader.WaveFormat.Channels is <1 or >2)throw new InvalidDataException(T["PublicationPreviewLimit"]);
                var temp=Path.Combine(store.Root,"cache","preview-"+Guid.NewGuid()+".wav");
                try{WaveFileWriter.CreateWaveFile16(temp,reader);return Convert.ToBase64String(File.ReadAllBytes(temp));}finally{if(File.Exists(temp))File.Delete(temp);}
            });
        }
        var values=tags.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(values.Length>8||values.Any(v=>v.Length>30))throw new InvalidDataException(T["PublicationTagsLimit"]);
        await community.EditPublication(new{id,title,description,tags=values,category,language,visibility,cover=image,preview=audio});
    }
}
