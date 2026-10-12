using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MyVoice.App.Services;
using MyVoice.Updater;
namespace MyVoice.App;
public enum StartupPhase { Initialize, CheckingUpdates, DownloadingUpdate, VerifyingUpdate, LoadingSettings, DetectingAudio, StartingAudioEngine, StartingAIEngine, LoadingLibrary, Ready, Error }
public sealed class SplashWindow : Window
{
    private readonly TextBlock status;
    private readonly ProgressBar progress;
    private readonly System.Windows.Shapes.Path spinner;
    private readonly Button continueButton;
    private readonly TextBlock detail;
    private readonly BrandMark mark;
    private readonly StackPanel panel;
    public event Action? ContinueRequested;
    public StartupPhase Phase {get;private set;}
    public SplashWindow()
    {
        Width=490;Height=370;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;AllowsTransparency=true;Background=Brushes.Transparent;
        Icon=new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory,"MyVoice.Signal.ico")));
        var frame=new Border{CornerRadius=new CornerRadius(24),Padding=new Thickness(36),BorderThickness=new Thickness(1)};
        frame.SetResourceReference(Border.BackgroundProperty,"CanvasBrush");frame.SetResourceReference(Border.BorderBrushProperty,"LineBrush");Content=frame;
        panel=new StackPanel();frame.Child=panel;
        mark=new BrandMark{Width=74,Height=74,HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(mark);
        panel.Children.Add(new BrandWordmark{Width=158,Height=38,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,5,0,24)});
        status=new TextBlock{Text="Vérification des mises à jour…",FontSize=15,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap};panel.Children.Add(status);
        spinner=new System.Windows.Shapes.Path{Width=20,Height=20,StrokeThickness=2,Data=Geometry.Parse("M 10,1 A 9,9 0 1 1 1,10"),RenderTransformOrigin=new Point(.5,.5),RenderTransform=new RotateTransform(),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,16,0,4)};
        spinner.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"AccentBrush");panel.Children.Add(spinner);
        progress=new ProgressBar{Minimum=0,Maximum=100,Height=5,Margin=new Thickness(0,16,0,4),Visibility=Visibility.Collapsed};panel.Children.Add(progress);
        detail=new TextBlock{TextAlignment=TextAlignment.Center,FontSize=11,Margin=new Thickness(0,6,0,0)};detail.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");panel.Children.Add(detail);
        continueButton=new Button{Content="Démarrer la version actuelle",HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,10,0,0),Visibility=Visibility.Collapsed};continueButton.Click+=(_,_)=>{continueButton.IsEnabled=false;ContinueRequested?.Invoke();};panel.Children.Add(continueButton);
        Loaded+=(_,_)=>RefreshMotion();
    }
    public void RefreshMotion()
    {
        mark.InvalidateVisual();
        ((RotateTransform)spinner.RenderTransform).BeginAnimation(RotateTransform.AngleProperty,ProductAppearance.Animate?new DoubleAnimation(0,360,TimeSpan.FromSeconds(1)){RepeatBehavior=RepeatBehavior.Forever}:null);
        mark.BeginAnimation(OpacityProperty,ProductAppearance.Animate?new DoubleAnimation(.65,1,TimeSpan.FromSeconds(1.8)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever}:null);
    }
    public void Stage(StartupPhase phase,string text)
    {
        var changed=Phase!=phase;Phase=phase;status.Text=text;progress.Visibility=Visibility.Collapsed;spinner.Visibility=phase is StartupPhase.Ready or StartupPhase.Error?Visibility.Collapsed:Visibility.Visible;detail.Text="";continueButton.Visibility=Visibility.Collapsed;
        if(changed&&ProductAppearance.Animate)status.BeginAnimation(OpacityProperty,new DoubleAnimation(.5,1,TimeSpan.FromMilliseconds(180)));
    }
    public void Update(UpdateSnapshot update)
    {
        Stage(update.Phase switch { UpdatePhase.Downloading=>StartupPhase.DownloadingUpdate,UpdatePhase.Verifying=>StartupPhase.VerifyingUpdate,UpdatePhase.Failed=>StartupPhase.Error,_=>StartupPhase.CheckingUpdates },update.Message);
        continueButton.Visibility=update.Phase is UpdatePhase.Downloading or UpdatePhase.Checking?Visibility.Visible:Visibility.Collapsed;
        if(update.Phase==UpdatePhase.Downloading&&update.Percent is {} value){spinner.Visibility=Visibility.Collapsed;progress.Visibility=Visibility.Visible;progress.Value=value;detail.Text=$"{value:0} %";}
        if(update.Phase is UpdatePhase.Current or UpdatePhase.Ready)spinner.Visibility=Visibility.Collapsed;
    }
    public async Task<bool> OfferRetryAsync(string message,Action? diagnostics=null)
    {
        Stage(StartupPhase.Error,message);Height=440;
        var answer=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,14,0,0)};
        var retry=new Button{Content="Réessayer"};retry.Click+=(_,_)=>answer.TrySetResult(true);actions.Children.Add(retry);
        var proceed=new Button{Content="Continuer"};proceed.Click+=(_,_)=>answer.TrySetResult(false);actions.Children.Add(proceed);
        if(diagnostics!=null){var logs=new Button{Content="Diagnostics"};logs.Click+=(_,_)=>diagnostics();actions.Children.Add(logs);}
        panel.Children.Add(actions);var result=await answer.Task;panel.Children.Remove(actions);Height=370;return result;
    }
    public async Task FinishAsync(string message="Prêt ✓"){Stage(StartupPhase.Ready,message);if(ProductAppearance.Animate){BeginAnimation(OpacityProperty,new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(160)));await Task.Delay(160);}Close();}
}
