using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MyVoice.App.Services;
public sealed class CoverImage : Image
{
    public static readonly DependencyProperty FilePathProperty=DependencyProperty.Register(nameof(FilePath),typeof(string),typeof(CoverImage),new PropertyMetadata(null,FileChanged));
    public string? FilePath {get=>(string?)GetValue(FilePathProperty);set=>SetValue(FilePathProperty,value);}
    private int generation;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,System.Windows.Media.Imaging.BitmapImage> Thumbnails=new();
    private static async void FileChanged(DependencyObject obj,DependencyPropertyChangedEventArgs args)
    {
        var image=(CoverImage)obj;var current=++image.generation;image.Source=null;
        if(args.NewValue is not string file||!System.IO.File.Exists(file))return;
        try{
            var key=file+System.IO.File.GetLastWriteTimeUtc(file).Ticks;
            if(!Thumbnails.TryGetValue(key,out var bitmap)){
                bitmap=await System.Threading.Tasks.Task.Run(()=>{var b=new System.Windows.Media.Imaging.BitmapImage();b.BeginInit();b.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;b.DecodePixelWidth=384;b.UriSource=new Uri(System.IO.Path.GetFullPath(file));b.EndInit();b.Freeze();return b;});
                if(Thumbnails.Count>96)Thumbnails.Clear();Thumbnails[key]=bitmap;
            }
            if(current==image.generation)image.Source=bitmap;
        }catch{if(current==image.generation)image.Source=null;}
    }

    public static readonly DependencyProperty CircularProperty = DependencyProperty.Register(nameof(Circular), typeof(bool), typeof(CoverImage), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool Circular { get => (bool)GetValue(CircularProperty); set => SetValue(CircularProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(RenderSize);
        dc.PushClip(Circular ? new EllipseGeometry(bounds) : new RectangleGeometry(bounds, 12, 12));
        base.OnRender(dc);
        dc.Pop();
    }
}
public sealed class HelpButton : Button
{
    public HelpButton()
    {
        Content="?"; Width=24; Height=24; Padding=new Thickness(0); Margin=new Thickness(8,0,0,0);
        VerticalAlignment=VerticalAlignment.Center;
        SetResourceReference(StyleProperty, "CircleButton");
        ToolTipService.SetInitialShowDelay(this, 150); ToolTipService.SetShowDuration(this, 30000);
        System.Windows.Automation.AutomationProperties.SetName(this, "Afficher l’aide");
        Click += (_,_) => { if(ToolTip is ToolTip popup) { popup.PlacementTarget=this; popup.IsOpen=!popup.IsOpen; } else { var tip=new ToolTip { Content=ToolTip, PlacementTarget=this, StaysOpen=false }; ToolTip=tip; tip.IsOpen=true; } };
    }
}
