using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MyVoice.App.Services;
public sealed class CoverImage : Image
{
    public static readonly DependencyProperty FilePathProperty=DependencyProperty.Register(nameof(FilePath),typeof(string),typeof(CoverImage),new PropertyMetadata(null,FileChanged));
    public string? FilePath {get=>(string?)GetValue(FilePathProperty);set=>SetValue(FilePathProperty,value);}
    private int generation,frame;
    private CoverFrames? frames;
    private readonly System.Windows.Threading.DispatcherTimer timer=new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,CoverFrames> Thumbnails=new();
    private static readonly System.Threading.SemaphoreSlim DecodeGate=new(2,2);
    public CoverImage()
    {
        Loaded+=(_,_)=>Load();Unloaded+=(_,_)=>{++generation;timer.Stop();frames=null;Source=null;};
        IsVisibleChanged+=(_,_)=>{if(IsVisible)Load();else timer.Stop();};
        timer.Tick+=(_,_)=>{if(frames==null||!IsVisible||!ProductAppearance.Animate){timer.Stop();return;}frame=(frame+1)%frames.Frames.Length;Source=frames.Frames[frame];timer.Interval=TimeSpan.FromMilliseconds(frames.Delays[frame]);};
    }
    private static void FileChanged(DependencyObject obj,DependencyPropertyChangedEventArgs args)=>((CoverImage)obj).Load();
    private async void Load()
    {
        int current=++generation;timer.Stop();Source=null;frames=null;frame=0;
        if(!IsLoaded||!IsVisible||FilePath is not string file||!System.IO.File.Exists(file))return;
        try
        {
            var key=file+System.IO.File.GetLastWriteTimeUtc(file).Ticks;
            if(!Thumbnails.TryGetValue(key,out var decoded))
            {
                await DecodeGate.WaitAsync();
                try
                {
                    if(current!=generation)return;
                    if(!Thumbnails.TryGetValue(key,out decoded))
                    {
                        decoded=await System.Threading.Tasks.Task.Run(()=>CoverDecoder.Read(file));
                        long bytes=0;foreach(var cached in Thumbnails.Values)foreach(var bitmap in cached.Frames)bytes+=(long)bitmap.PixelWidth*bitmap.PixelHeight*4;
                        long added=0;foreach(var bitmap in decoded.Frames)added+=(long)bitmap.PixelWidth*bitmap.PixelHeight*4;
                        if(Thumbnails.Count>=96||bytes+added>64*1024*1024)Thumbnails.Clear();Thumbnails[key]=decoded;
                    }
                }
                finally{DecodeGate.Release();}
            }
            if(current!=generation)return;
            frames=decoded;Source=frames.Frames[0];
            if(frames.Frames.Length>1&&ProductAppearance.Animate){timer.Interval=TimeSpan.FromMilliseconds(frames.Delays[0]);timer.Start();}
        }
        catch{if(current==generation)Source=null;}
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
