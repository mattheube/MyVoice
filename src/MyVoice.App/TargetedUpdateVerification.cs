using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.Core;
using MyVoice.AI;
using NAudio.Wave;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyTargetedUpdate(MainWindow window,string folder)
    {
        var count=vm!.AiVoices.Count;
        await vm.PreparePresetPublication(new(){Name="Draft verification"});
        vm.PublishDescription="Saved description";vm.ResolvePublicationClose(null);
        if(!vm.ShowPublication||!vm.HasPublicationPackage)throw new Exception("Cancel discarded editor");
        vm.ResolvePublicationClose(true);
        if(vm.ShowPublication||!vm.HasPublicationPackage)throw new Exception("Save lost draft");
        vm.OpenPublicationCommand.Execute(null);vm.PublishDescription="Discarded draft";vm.ResolvePublicationClose(false);
        if(vm.ShowPublication||vm.HasPublicationPackage||vm.AiVoices.Count!=count)throw new Exception("Discard affected original voice or retained draft");
        var originalTheme=vm.Theme;vm.MotionMode="Full";Services.UiActivity.Set(Services.UiActivityState.Foreground);
        vm.Theme="Deep Ocean";vm.LiquidGlass=false;var original=((SolidColorBrush)Current.Resources["SurfaceBrush"]).Color;
        vm.LiquidGlass=true;
        if((RenderCapability.Tier>>16)>0&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast&&Current.Resources["SurfaceBrush"] is not LinearGradientBrush)throw new Exception("Glass missing");
        vm.Page=3;window.UpdateLayout();Capture("glass-ocean.png");
        Services.UiActivity.Set(Services.UiActivityState.Tray);if(Services.ProductAppearance.GlassActive)throw new Exception("Glass active in tray");
        Services.UiActivity.Set(Services.UiActivityState.Foreground);vm.LiquidGlass=false;
        if(((SolidColorBrush)Current.Resources["SurfaceBrush"]).Color!=original)throw new Exception("Glass OFF did not restore theme");
        vm.Theme=originalTheme;var savedDevice=vm.AiDevice;var savedMode=vm.AiModel;var savedProfile=vm.AiProfile;vm.LanguageIndex=1;vm.Page=2;window.UpdateLayout();Capture("studio-english.png");
        if(vm.AiDevice!=savedDevice||vm.AiModel!=savedMode||vm.AiProfile!=savedProfile)throw new Exception("Language changed engine selections");
        if(vm.T["AudioConversion"]!="Audio Conversion")throw new Exception("Language did not apply");
        var tabs=Children(window).OfType<System.Windows.Controls.TabControl>().First(t=>t.Items.OfType<System.Windows.Controls.TabItem>().Any(i=>i.Header?.ToString()==vm.T["TextToVoice"]));
        tabs.SelectedIndex=1;window.UpdateLayout();tabs.BringIntoView();await Task.Delay(150);Capture("audio-english.png");
        tabs.SelectedIndex=2;window.UpdateLayout();tabs.BringIntoView();await Task.Delay(150);Capture("text-english.png");
        foreach(var combo in Children(tabs).OfType<System.Windows.Controls.ComboBox>().Where(c=>c.SelectedValuePath=="Id"))if(combo.SelectedItem==null)throw new Exception("Empty Studio choice after language switch");
        vm.LanguageIndex=0;window.UpdateLayout();Capture("text-french.png");
        vm.ShowWelcome=true;window.UpdateLayout();Capture("setup-language.png");vm.ShowWelcome=false;
        var source=Path.Combine(folder,"format-source.wav");
        using(var writer=new WaveFileWriter(source,WaveFormat.CreateIeeeFloatWaveFormat(48000,1))){var audio=Enumerable.Range(0,96000).Select(i=>(float)(.15*Math.Sin(i*.055))).ToArray();writer.WriteSamples(audio,0,audio.Length);}
        foreach(var format in new[]{"wav","flac","mp3","ogg","opus"}){
            var encoded=Path.Combine(folder,"format."+format);var decoded=Path.Combine(folder,format+"-decoded.wav");
            await Task.Run(()=>{AudioCodec.Export(source,encoded,format,48000,default);AudioCodec.Decode(encoded,decoded,default);});
            using var reader=new AudioFileReader(decoded);if(reader.TotalTime.TotalSeconds<1.8)throw new Exception("Codec lost audio: "+format);
        }
        foreach(var format in new[]{"m4a","aac"}){
            var encoded=Path.Combine(folder,"format."+format);var decoded=Path.Combine(folder,format+"-decoded.wav");
            var info=new System.Diagnostics.ProcessStartInfo(AudioCodec.Executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-nostdin","-v","error","-y","-i",source,"-c:a","aac",encoded})info.ArgumentList.Add(arg);
            using var process=System.Diagnostics.Process.Start(info)!;var errors=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();if(process.ExitCode!=0)throw new Exception(await errors);
            await Task.Run(()=>AudioCodec.Decode(encoded,decoded,default));using var reader=new AudioFileReader(decoded);if(reader.TotalTime.TotalSeconds<1.8)throw new Exception("AAC decode lost audio");
        }
        var voice=new VoiceReference{Name="Drop verification",ReferenceFile=source,Duration=2};vm.AiVoices.Add(voice);vm.SelectedAiVoice=voice;
        var wrong=Path.Combine(folder,"wrong.mp3");File.WriteAllBytes(wrong,new byte[]{137,80,78,71,13,10,26,10});
        await vm.DropVoiceSamples([Path.Combine(folder,"format.ogg"),wrong]);
        if(voice.ReferenceFile!=source||voice.Revision!=1||voice.ActiveReferenceFiles.Length!=1||voice.ActiveReferenceFiles[0]!=source||voice.Samples.Any(s=>s.Name=="wrong.mp3")||!voice.Samples.Any(s=>s.Name=="format.ogg"))throw new Exception("Mixed drop changed active voice or accepted image");
        var avatar=vm.ProfileAvatar;vm.DropImage("avatar",source);if(vm.ProfileAvatar!=avatar)throw new Exception("Image drop accepted audio");
        var soundCount=vm.SoundCards.Count;await vm.ImportPathsAsync([Path.Combine(folder,"format.m4a"),Path.Combine(folder,"format.opus"),wrong]);if(vm.SoundCards.Count!=soundCount+2)throw new Exception("Mixed sound import rejected valid formats or accepted image");
        var stereo=Path.Combine(folder,"stereo.wav");using(var writer=new WaveFileWriter(stereo,WaveFormat.CreateIeeeFloatWaveFormat(48000,2))){var frames=Enumerable.Range(0,96000).Select(i=>i%2==0?.2f:-.1f).ToArray();writer.WriteSamples(frames,0,frames.Length);}
        await vm.ImportPathsAsync([stereo]);using(var reader=new AudioFileReader(vm.SoundCards.Last().Item.File)){var data=new float[2];reader.Read(data,0,2);if(reader.WaveFormat.Channels!=2||Math.Abs(data[0]-.2)>.001||Math.Abs(data[1]+.1)>.001)throw new Exception("Sound import collapsed stereo channels");}
        await vm.VerifyAudioWorkflow(folder);
        File.WriteAllText(Path.Combine(folder,"targeted-update-result.txt"),"PASS draft Cancel/Save/Discard; original voice preserved; Glass theme/restore/tray; language/setup/choice stability; WAV/FLAC/MP3/OGG/OPUS encode/decode; M4A/AAC decode; mixed voice and sound import; audio rejected by image target; no automatic voice update. See audio-workflow-result.txt for physical microphone and bus tests. Perceptual quality is not asserted by this test.");
        System.Collections.Generic.IEnumerable<DependencyObject> Children(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var item in Children(child))yield return item;}}
        void Capture(string name){var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(folder,name));encoder.Save(stream);}
    }
}

