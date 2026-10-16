using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.App.Services;
using MyVoice.Core;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyMenus(MainWindow window,string folder)
    {
        object[] fixtures=[vm!.SoundCards.First(),new VoiceReference{Name="AI fixture"},new DesignedVoice{Name="Preset fixture"}];
        foreach(string theme in new[]{"Graphite","Deep Ocean","Crimson"})
        {
            ProductAppearance.Apply(new ProductState{Theme=theme,Motion="Reduced"});
            for(int kind=0;kind<fixtures.Length;kind++)foreach(double scale in new[]{1d,1.25,1.5,2})
            {
                var menu=LibraryContextMenu.Build(vm,fixtures[kind]);menu.PlacementTarget=window;menu.IsOpen=true;await Task.Delay(60);menu.UpdateLayout();
                foreach(var item in menu.Items.OfType<MenuItem>())if(item.Icon is FrameworkElement icon&&(icon.ActualWidth!=16||icon.ActualHeight!=16))throw new Exception("Stretched context icon");
                int w=(int)Math.Ceiling(menu.ActualWidth*scale),h=(int)Math.Ceiling(menu.ActualHeight*scale);
                var bitmap=new RenderTargetBitmap(w,h,96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(menu);
                var pixels=new byte[w*h*4];bitmap.CopyPixels(pixels,w*4,0);
                // The old system gutter was a solid light strip across almost the entire menu height.
                int white=0;for(int y=0;y<h;y++){int p=(y*w+(int)(8*scale))*4;if(pixels[p]>225&&pixels[p+1]>225&&pixels[p+2]>225&&pixels[p+3]>225)white++;}
                if(white>h/2)throw new Exception("System context menu gutter is still visible");
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(folder,$"menu-{theme}-{kind}-{scale*100:0}.png")))encoder.Save(stream);
                var collection=menu.Items.OfType<MenuItem>().First(i=>i.HasItems);collection.Focus();collection.IsSubmenuOpen=true;menu.UpdateLayout();if(!collection.IsSubmenuOpen)throw new Exception("Collection submenu did not open");
                menu.IsOpen=false;
            }
        }
        File.WriteAllText(Path.Combine(folder,"menu-result.txt"),"PASS sound/AI/preset menus; three themes; raster scales 100/125/150/200%; 16px icons; no system gutter; collection submenus. Physical multi-monitor DPI changes not tested.");
    }
}
