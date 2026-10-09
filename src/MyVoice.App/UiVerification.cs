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
    private void VerifyProductUi(string folder)
    {
        vm!.DesignerName="Smoke custom voice";vm.DesignerPitch=1.15;vm.DesignerBass=3;vm.SaveDesignedVoiceCommand.Execute(null);
        var voice=vm.DesignedVoices[^1];vm.UseDesignedVoiceCommand.Execute(voice);
        if(vm.DisplayVoice!=voice.Name||vm.Presets.Any(p=>p.Selected))throw new Exception("Custom selection is inconsistent");
        vm.SelectPresetCommand.Execute(vm.Presets[0]);
        if(vm.DisplayVoice!="Default Clean"||vm.LastVoiceName!=voice.Name)throw new Exception("Clean lost last custom voice");
        vm.QuickSwitchVoiceCommand.Execute(null);
        if(vm.DisplayVoice!=voice.Name)throw new Exception("Last custom voice not restored");
        foreach(var theme in vm.Themes){vm.Theme=theme;if(vm.Theme!=theme)throw new Exception("Theme not applied");}
        vm.Theme="Midnight";vm.MotionMode="Off";vm.ContinueLocallyCommand.Execute(null);
        var reloaded=new MyVoice.Infrastructure.ProductStore(store!.Root).Load();
        if(!reloaded.OnboardingCompleted||reloaded.Motion!="Off"||!reloaded.Presets.Any(p=>p.Id==voice.Id))throw new Exception("Product preferences not persistent");
        File.WriteAllText(Path.Combine(folder,"product-result.txt"),"PASS: designer save, selection, Clean/last voice, four themes, reduced motion, onboarding and product persistence.");
    }
    private async Task VerifyImageUi(Window owner,string folder)
    {
        if(vm!.SelectedSound!=null)
        {
            vm.SoundVolume=1234;vm.SoundboardVolume=987;vm.MonitoringVolume=345;
            owner.UpdateLayout();await Task.Delay(100);
            if(Math.Abs(vm.SoundVolume-1234)>.001||Math.Abs(vm.SoundboardVolume-987)>.001||Math.Abs(vm.MonitoringVolume-345)>.001)throw new Exception("Slider clamped numeric volume");
            vm.Save();if(store!.Load().SoundboardVolume!=9.87)throw new Exception("Numeric volume not persisted");
            vm.SoundVolume=100;vm.SoundboardVolume=100;vm.MonitoringVolume=50;
        }
        void Save(Visual visual,int width,int height,string name)
        {
            var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bmp.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(Path.Combine(folder,name));encoder.Save(file);
        }
        var pattern=new DrawingVisual();using(var dc=pattern.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Coral,null,new Rect(0,0,200,300));dc.DrawRectangle(Brushes.MediumSeaGreen,null,new Rect(200,0,200,300));dc.DrawRectangle(Brushes.SteelBlue,null,new Rect(400,0,200,300));
        }
        Save(pattern,600,300,"crop-source.png");
        foreach(var round in new[]{false,true})
        {
            var crop=new ImageCropWindow(Path.Combine(folder,"crop-source.png"),round){Owner=owner};crop.Show();await Task.Delay(180);crop.UpdateLayout();
            Save(crop,(int)crop.ActualWidth,(int)crop.ActualHeight,round?"crop-circle.png":"crop-square.png");
            var image=crop.Export();if(image.PixelWidth!=512||image.PixelHeight!=512)throw new Exception("Crop output is not square");
            var center=new byte[4];image.CopyPixels(new Int32Rect(256,256,1,1),center,4,0);if(center[1]<=center[0]||center[1]<=center[2])throw new Exception("Crop does not preserve image center");
            crop.Close();
        }
        var tip=new ToolTip {Content="Automatique choisit la carte NVIDIA si elle est disponible. Le processeur reste compatible, mais plus lent pour le direct.",PlacementTarget=owner,IsOpen=true,StaysOpen=true};
        await Task.Delay(200);tip.UpdateLayout();Save(tip,(int)tip.ActualWidth,(int)tip.ActualHeight,"help-tooltip.png");tip.IsOpen=false;
        File.WriteAllText(Path.Combine(folder,"image-help-result.txt"),"PASS square/circle crop dialogs, centered 512px export, themed tooltip rendered.");
    }
}
