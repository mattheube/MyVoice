using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.Updater;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyStartupUi(MainWindow window,string folder)
    {
        var virtualBus=System.Linq.Enumerable.Single(vm!.MixerOutputs,b=>b.Id=="virtual");
        virtualBus.Device="@default";
        if(System.Linq.Enumerable.Single(vm.MixerOutputs,b=>b.Id=="virtual").Device=="@default")throw new Exception("Virtual bus allowed speaker/default override");
        static void Shot(FrameworkElement element,string path)
        {
            element.UpdateLayout();var bmp=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(element);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(path);png.Save(file);
        }
        var bounds=new Rect(window.Left,window.Top,window.Width,window.Height);
        window.WindowState=WindowState.Maximized;await Task.Delay(200);window.VerifyNativeBounds(false);Shot(window,Path.Combine(folder,"maximized.png"));
        window.ToggleFullscreen();await Task.Delay(200);window.VerifyNativeBounds(true);Shot(window,Path.Combine(folder,"fullscreen.png"));
        window.ToggleFullscreen();await Task.Delay(200);if(window.WindowState!=WindowState.Maximized)throw new Exception("Fullscreen did not restore maximized state");window.VerifyNativeBounds(false);
        window.WindowState=WindowState.Normal;window.Left=bounds.Left;window.Top=bounds.Top;window.Width=bounds.Width;window.Height=bounds.Height;
        window.ToggleFullscreen();await Task.Delay(100);window.ToggleFullscreen();await Task.Delay(100);
        if(Math.Abs(window.Left-bounds.Left)>1||Math.Abs(window.Top-bounds.Top)>1||Math.Abs(window.Width-bounds.Width)>1||Math.Abs(window.Height-bounds.Height)>1)throw new Exception("Fullscreen did not restore normal bounds");
        vm!.Theme="Deep Ocean";vm.Density="Compact";vm.MotionMode="Off";
        var splash=new SplashWindow();splash.Show();await Task.Delay(80);Shot(splash,Path.Combine(folder,"startup-checking.png"));
        splash.Update(new(UpdatePhase.Downloading,"MyVoice · aperçu du téléchargement",67));await Task.Delay(80);Shot(splash,Path.Combine(folder,"startup-progress.png"));
        splash.Stage(StartupPhase.Ready,"Prêt ✓");await Task.Delay(80);Shot(splash,Path.Combine(folder,"startup-ready.png"));splash.Close();
        vm.Page=6;await Task.Delay(80);Shot(window,Path.Combine(folder,"mic-ocean.png"));
        var saved=new MyVoice.Infrastructure.ProductStore(store!.Root).Load();if(saved.Theme!="Deep Ocean"||saved.Density!="Compact")throw new Exception("Theme/density not persisted");
        vm.ApplyMicPresetCommand.Execute("Distant Microphone");if(vm.GainDb!=6||vm.GateThreshold!=-58||vm.CompressorRatio!=2)throw new Exception("Mic preset not applied");
        vm.ResetProcessingCommand.Execute(null);vm.Theme="Midnight";vm.Density="Comfortable";
        File.WriteAllText(Path.Combine(folder,"startup-window-result.txt"),"PASS actual monitor work area; fullscreen physical bounds; restore normal and maximized; splash state renders; Ocean and Compact persistence; microphone preset uses real parameters.\nMulti-monitor and mixed-DPI transitions require physical validation.");
    }
}
