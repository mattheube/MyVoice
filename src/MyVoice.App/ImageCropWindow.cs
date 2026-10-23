using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace MyVoice.App;
public sealed class ImageCropWindow : Window
{
    private BitmapSource bitmap=null!;
    private readonly Canvas viewport=new() { Width=360, Height=360, Background=Brushes.Black, ClipToBounds=true };
    private readonly Image preview=new() { Stretch=Stretch.Fill };
    private readonly Slider zoom=new() { Minimum=1, Maximum=4, Value=1, Width=270 };
    private double offsetX,offsetY,scale;
    private Point? drag;
    private int rotation;
    public string SourceFile { get; private set; }
    public BitmapSource? Result { get; private set; }
    public ImageCropWindow(string file, bool circular, double aspectRatio=1)
    {
        viewport.Height=360/Math.Clamp(aspectRatio,1,3);
        SourceFile=file; Title=circular?"Cadrer la photo de la voix":"Cadrer l’image du son";
        Width=510;Height=740;MaxHeight=SystemParameters.WorkArea.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(28,29,32)); Foreground=Brushes.WhiteSmoke;
        Icon=new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory,"MyVoice.ico")));
        var panel=new StackPanel { Margin=new Thickness(24) }; Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text=Title, FontSize=22, Margin=new Thickness(0,0,0,8) });
        panel.Children.Add(new TextBlock { Text="Déplacez l’image avec la souris, puis ajustez le zoom.", Margin=new Thickness(0,0,0,16) });
        viewport.Children.Add(preview);
        viewport.Clip=circular?new EllipseGeometry(new Rect(0,0,viewport.Width,viewport.Height)):new RectangleGeometry(new Rect(0,0,viewport.Width,viewport.Height),12,12);
        panel.Children.Add(viewport);
        var controls=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=new Thickness(0,12,0,0) };
        controls.Children.Add(new TextBlock { Text="Zoom", VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(0,0,12,0) }); controls.Children.Add(zoom); panel.Children.Add(controls);
        var actions=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Center };panel.Children.Add(actions);
        void Button(string title, Action run) { var b=new Button { Content=title };b.Click+=(_,_)=>run();actions.Children.Add(b); }
        Button("Autre image",()=>{ var picker=new OpenFileDialog { Filter="Images|*.png;*.jpg;*.jpeg;*.webp;*.gif" };if(picker.ShowDialog(this)==true)Load(picker.FileName); });
        Button("Tourner",()=>{rotation=(rotation+90)%360;bitmap=new TransformedBitmap(bitmap,new RotateTransform(90));Reset();});
        Button("Recentrer",Reset);
        var bottom=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,10,0,0) };panel.Children.Add(bottom);
        var cancel=new Button { Content="Annuler",IsCancel=true };bottom.Children.Add(cancel);
        var save=new Button { Content="Utiliser ce cadrage",IsDefault=true };save.SetResourceReference(StyleProperty,"PrimaryButton");bottom.Children.Add(save);
        save.Click+=(_,_)=>{ Result=Export();DialogResult=true; };
        zoom.ValueChanged+=(_,_)=>Update();
        viewport.MouseLeftButtonDown+=(_,e)=>{drag=e.GetPosition(viewport);viewport.CaptureMouse();};
        viewport.MouseMove+=(_,e)=>{ if(drag is not Point previous)return;var current=e.GetPosition(viewport);offsetX+=current.X-previous.X;offsetY+=current.Y-previous.Y;drag=current;Update(); };
        viewport.MouseLeftButtonUp+=(_,_)=>{drag=null;viewport.ReleaseMouseCapture();};
        viewport.LostMouseCapture+=(_,_)=>drag=null;
        Load(file);
    }
    private void Load(string file)
    {
        if(new FileInfo(file).Length>20_000_000)throw new InvalidDataException("Image limitée à 20 Mo.");
        var image=Services.CoverDecoder.Read(file,2048,false).Frames[0];
        bitmap=image;SourceFile=file;rotation=0;Reset();
    }
    private void Reset(){offsetX=offsetY=0;zoom.Value=1;Update();}
    private void Update()
    {
        if(bitmap==null)return;
        scale=Math.Max(viewport.Width/bitmap.PixelWidth,viewport.Height/bitmap.PixelHeight)*zoom.Value;
        var width=bitmap.PixelWidth*scale;var height=bitmap.PixelHeight*scale;
        offsetX=Math.Clamp(offsetX,-(width-360)/2,(width-360)/2);offsetY=Math.Clamp(offsetY,-(height-viewport.Height)/2,(height-viewport.Height)/2);
        preview.Source=bitmap;preview.Width=width;preview.Height=height;
        Canvas.SetLeft(preview,(360-width)/2+offsetX);Canvas.SetTop(preview,(viewport.Height-height)/2+offsetY);
    }
    public BitmapSource Export()
    {
        int width=Math.Clamp((int)Math.Round(viewport.Width/scale),1,bitmap.PixelWidth);
        int height=Math.Clamp((int)Math.Round(viewport.Height/scale),1,bitmap.PixelHeight);
        int x=Math.Clamp((int)Math.Round(-Canvas.GetLeft(preview)/scale),0,bitmap.PixelWidth-width);
        int y=Math.Clamp((int)Math.Round(-Canvas.GetTop(preview)/scale),0,bitmap.PixelHeight-height);
        var cropped=new CroppedBitmap(bitmap,new Int32Rect(x,y,width,height));
        int outWidth=viewport.Height<360?1024:512,outHeight=(int)Math.Round(outWidth*viewport.Height/viewport.Width);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(cropped,new Rect(0,0,outWidth,outHeight));
        var output=new RenderTargetBitmap(outWidth,outHeight,96,96,PixelFormats.Pbgra32);output.Render(visual);output.Freeze();return output;
    }
    public string SaveExport(string basePath)
    {
        var decoded=Services.CoverDecoder.Read(SourceFile,512);
        BitmapEncoder encoder;
        string extension;
        if(decoded.Frames.Length==1||viewport.Height<360)
        {encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Result??Export()));extension=".png";}
        else
        {
            encoder=new GifBitmapEncoder();extension=".gif";
            double side=Math.Min(1,360/scale/Math.Min(bitmap.PixelWidth,bitmap.PixelHeight));
            double left=Math.Max(0,-Canvas.GetLeft(preview)/scale/bitmap.PixelWidth),top=Math.Max(0,-Canvas.GetTop(preview)/scale/bitmap.PixelHeight);
            for(int i=0;i<decoded.Frames.Length;i++)
            {
                BitmapSource frame=decoded.Frames[i];if(rotation!=0)frame=new TransformedBitmap(frame,new RotateTransform(rotation));
                int length=Math.Max(1,(int)(Math.Min(frame.PixelWidth,frame.PixelHeight)*side));
                var cropped=new CroppedBitmap(frame,new Int32Rect(Math.Clamp((int)(left*frame.PixelWidth),0,frame.PixelWidth-length),Math.Clamp((int)(top*frame.PixelHeight),0,frame.PixelHeight-length),length,length));
                var metadata=new BitmapMetadata("gif");metadata.SetQuery("/grctlext/Delay",(ushort)(decoded.Delays[i]/10));metadata.SetQuery("/grctlext/Disposal",(byte)2);
                encoder.Frames.Add(BitmapFrame.Create(cropped,null,metadata,null));
            }
        }
        var path=basePath+extension;
        if(encoder is GifBitmapEncoder gif)Services.GifEncoding.Save(path,gif,decoded.Delays);
        else using(var file=File.Create(path))encoder.Save(file);
        return path;
    }
}
