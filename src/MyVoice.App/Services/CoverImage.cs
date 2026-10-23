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
    private System.Threading.CancellationTokenSource? decodeCancellation;
    private void CancelDecode(){decodeCancellation?.Cancel();decodeCancellation?.Dispose();decodeCancellation=null;}
    private System.Windows.Threading.DispatcherTimer? timer;

    private static readonly System.Threading.SemaphoreSlim DecodeGate=new(2,2);
    public CoverImage()
    {
        Loaded+=(_,_)=>{UiActivity.Changed+=ActivityChanged;Load();};Unloaded+=(_,_)=>{UiActivity.Changed-=ActivityChanged;++generation;CancelDecode();timer?.Stop();frames=null;Source=null;};
        IsVisibleChanged+=(_,_)=>{if(IsVisible)Load();else {++generation;CancelDecode();timer?.Stop();frames=null;Source=null;}};

    }
    private void AnimateFrame(object? sender,EventArgs e)
    {
        if(frames==null||!IsVisible||!ProductAppearance.Animate||!UiActivity.VisualsActive){timer?.Stop();return;}
        frame=(frame+1)%frames.Frames.Length;Source=frames.Frames[frame];timer!.Interval=TimeSpan.FromMilliseconds(frames.Delays[frame]);
    }
    private void StartAnimation()
    {
        if(frames==null)return;
        if(timer==null){timer=new System.Windows.Threading.DispatcherTimer();timer.Tick+=AnimateFrame;}
        timer.Interval=TimeSpan.FromMilliseconds(frames.Delays[0]);timer.Start();
    }
    private void ActivityChanged(object? sender,EventArgs e){if(UiActivity.VisualsActive&&IsVisible)Load();else timer?.Stop();}
    private static void FileChanged(DependencyObject obj,DependencyPropertyChangedEventArgs args)=>((CoverImage)obj).Load();
    private async void Load()
    {
        int current=++generation;CancelDecode();timer?.Stop();Source=null;frames=null;frame=0;
        if(!IsLoaded||!IsVisible||FilePath is not string file||!System.IO.File.Exists(file))return;
        try
        {
            decodeCancellation=new System.Threading.CancellationTokenSource();var token=decodeCancellation.Token;
            double display=Math.Max(ActualWidth,ActualHeight)*VisualTreeHelper.GetDpi(this).DpiScaleX;
            int size=display>0&&double.IsFinite(display)?Math.Clamp((int)Math.Ceiling(display/32)*32,64,512):256;
            var key=file+"|"+System.IO.File.GetLastWriteTimeUtc(file).Ticks+"|"+size;
            if(!ThumbnailCache.TryGetValue(key,out var decoded))
            {
                await DecodeGate.WaitAsync(token);
                try
                {
                    if(current!=generation)return;
                    if(!ThumbnailCache.TryGetValue(key,out decoded))
                    {
                        decoded=await System.Threading.Tasks.Task.Run(()=>CoverDecoder.Read(file,size),token);
                        ThumbnailCache.Add(key,decoded);
                    }
                }
                finally{DecodeGate.Release();}
            }
            if(current!=generation)return;
            frames=decoded;Source=frames.Frames[0];
            DependencyObject? parent=this;
            while((parent=VisualTreeHelper.GetParent(parent))!=null)if(parent is CreationImageButton button){if(decoded.Accent is Color accent){var brush=new SolidColorBrush(accent);brush.Freeze();button.Resources["AccentBrush"]=brush;}else button.Resources.Remove("AccentBrush");break;}
            if(frames.Frames.Length>1&&ProductAppearance.Animate&&UiActivity.VisualsActive)StartAnimation();
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
