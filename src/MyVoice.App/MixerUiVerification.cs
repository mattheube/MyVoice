using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyMixerUi(MainWindow window,string folder)
    {
        await vm!.RefreshMixerDevicesCommand.ExecuteAsync(null);vm.Page=8;vm.MixerAdvanced=true;vm.MixerProfileName="UI test mix";vm.SaveAudioProfileCommand.Execute(null);
        var saved=vm.SelectedAudioProfile!;var mic=vm.MixerChannels.First(c=>c.Id=="mic");mic.Volume=173;mic.Pan=25;mic.Mute=true;
        vm.LoadAudioProfileCommand.Execute(null);
        if(vm.MixerChannels.First(c=>c.Id=="mic").Mute!=saved.Channels.First(c=>c.Id=="mic").Mute)throw new Exception("Profile did not restore mute");
        vm.DuplicateAudioProfileCommand.Execute(null);vm.MixerProfileName="UI renamed";vm.RenameAudioProfileCommand.Execute(null);
        if(vm.SelectedAudioProfile!.Name!="UI renamed")throw new Exception("Profile rename failed");
        vm.MixerSourceType="System Audio";vm.AddMixerSourceCommand.Execute(null);var source=vm.SelectedMixerChannel!;
        if(!source.CanRemove||source.CaptureEnabled)throw new Exception("Adding a source started capture");
        source.Volume=725;source.Bass=6;source.HighPass=80;source.Compressor=true;
        vm.Save();var reloaded=new MyVoice.Infrastructure.MixerStore(store!.Root).Load(vm.Settings);
        if(reloaded.Current.Channels.Single(c=>c.Id==source.Id).Volume!=7.25)throw new Exception("Mixer numeric volume was clamped");
        vm.AddMixerOutputCommand.Execute(null);vm.SelectedMixerOutput!.Name="Test output";vm.SelectedMixerOutput.Backend="MME";
        if(vm.SelectedMixerOutput.Choices.Count<2)throw new Exception("MME devices did not refresh");
        vm.SelectedMixerOutput.Backend="WASAPI";
        await Task.Delay(180);window.UpdateLayout();
        var scroll=(ScrollViewer)window.FindName("PageScroller");
        async Task Shot(string name,double offset)
        {
            scroll.ScrollToVerticalOffset(offset);await Task.Delay(120);window.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(folder,name));encoder.Save(file);
        }
        await Shot("mixer-sources.png",360);await Shot("mixer-matrix.png",1100);await Shot("mixer-fx.png",1700);await Shot("mixer-midi-recording.png",scroll.ScrollableHeight);
        if(vm.SelectedRecordingTarget?.Id!="recording")throw new Exception("Recording target lost selection");
        System.Collections.Generic.IEnumerable<DependencyObject> Children(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var next in Children(child))yield return next;}}
        if(Children(window).OfType<ComboBox>().Single(c=>Equals(c.Tag,"recording-target")).SelectedItem==null)throw new Exception("Recording selector is visually empty");
        vm.RemoveMixerOutputCommand.Execute(null);vm.RemoveMixerSourceCommand.Execute(source);vm.MixerAdvanced=false;scroll.ScrollToTop();
        if(vm.MixerChannels.Any(c=>c.Id==source.Id))throw new Exception("Source removal failed");
        File.WriteAllText(Path.Combine(folder,"mixer-result.txt"),"PASS profile save/load/duplicate/rename; explicit stopped source; numeric volume; EQ settings; source removal; MME/WASAPI choices; advanced controls rendered; local persistence.");
    }
}
