using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.App.Services;
using MyVoice.App.ViewModels;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyAppearanceCards(MainWindow window,string folder)
    {
        var previous=vm!.Product.Accent;
        try
        {
            vm.Product.Accent="#26D9A0";ProductAppearance.Apply(vm.Product);
            var icon=(BitmapSource)window.Icon;var pixels=new byte[64*64*4];icon.CopyPixels(pixels,256,0);
            if(!Enumerable.Range(0,4096).Any(i=>pixels[i*4+3]==255&&pixels[i*4+2]==38&&pixels[i*4+1]==217))throw new Exception("Window icon did not follow the accent");
            if(tray?.Icon==null)throw new Exception("Tray icon missing");
            using(var trayBitmap=tray.Icon.ToBitmap()){if(!Enumerable.Range(0,64).Any(y=>Enumerable.Range(0,64).Any(x=>{var c=trayBitmap.GetPixel(x,y);return c.A==255&&c.R==38&&c.G==217;})))throw new Exception("Tray icon did not follow the accent");}
            var host=new Window{Width=650,Height=420,DataContext=vm,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterScreen};
            host.SetResourceReference(Window.BackgroundProperty,"CanvasBrush");host.SetResourceReference(Window.ForegroundProperty,"TextBrush");
            var panel=new WrapPanel{Margin=new Thickness(24)};host.Content=panel;
            foreach(var kind in new[]{"soundboard","ai-voice","voice-preset"})panel.Children.Add(new Views.CreationCard{DataContext=new CommunityCard{Title=kind=="soundboard"?"My Sound":"Ma voix",Kind=kind,Username="myvoice",Cover=vm.SoundCards.FirstOrDefault()?.Cover}});
            host.Show();host.UpdateLayout();await Task.Delay(250);
            foreach(var card in panel.Children.OfType<Views.CreationCard>())
            {
                var button=Walk(card).OfType<Button>().First();
                if(Math.Abs(button.ActualWidth-button.ActualHeight)>.1)throw new Exception("Image button is not square/circular");
                if(Walk(button).OfType<TextBlock>().Any())throw new Exception("A title remains inside an image button");
                var cover=Walk(button).OfType<CoverImage>().Single();
                if(cover.Circular && button.InputHitTest(new Point(1,1))!=null)throw new Exception("Circle button accepts clicks in square corners");
                if(cover.Circular!=((CommunityCard)card.DataContext).IsVoice)throw new Exception("Incorrect cover shape");
            }
            var bitmap=new RenderTargetBitmap(650,420,96,96,PixelFormats.Pbgra32);bitmap.Render(host);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(folder,"creation-cards.png")))png.Save(file);host.Close();
            File.WriteAllText(Path.Combine(folder,"appearance-cards-result.txt"),"PASS window/tray accent pixels; square dimensions; circle voice covers; titles outside image buttons; shared Community/Profile card rendering.");
        }
        finally{vm.Product.Accent=previous;ProductAppearance.Apply(vm.Product);}
    }
}
