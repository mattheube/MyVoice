using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using MyVoice.App.Services;
using MyVoice.App.ViewModels;
namespace MyVoice.App;
public partial class MainWindow : Window
{
    private readonly MainViewModel vm; private HotkeyService? hotkey;
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        this.vm = vm;
        DataContext = vm;
        Width=Math.Clamp(vm.Settings.WindowWidth,MinWidth,Math.Max(MinWidth,SystemParameters.WorkArea.Width));
        Height=Math.Clamp(vm.Settings.WindowHeight,MinHeight,Math.Max(MinHeight,SystemParameters.WorkArea.Height));
        if(vm.Settings.WindowMaximized)WindowState=WindowState.Maximized;
        SizeChanged+=(_,_)=>{PlayerPanel.Width=ActualWidth<1150?230:270;};
        vm.PropertyChanged += Changed;
        SourceInitialized += (_, _) => { hotkey = new(new WindowInteropHelper(this).Handle); vm.AttachHotkey(hotkey); };
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(vm.NowPlayingVisible)&&vm.NowPlayingVisible&&ProductAppearance.Animate)PlayerPanel.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));
        if (e.PropertyName == nameof(vm.Page))
        {
            PageScroller.ScrollToTop();
            if (ProductAppearance.Animate)
                PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        }
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        vm.Settings.WindowMaximized=WindowState==WindowState.Maximized;
        if(WindowState==WindowState.Normal){vm.Settings.WindowWidth=ActualWidth;vm.Settings.WindowHeight=ActualHeight;}
        vm.Save();
        if (vm.CloseToTray && !((App)Application.Current).Exiting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        vm.PropertyChanged -= Changed;
        hotkey?.Dispose();
        base.OnClosed(e);
        if (!((App)Application.Current).Exiting)
            ((App)Application.Current).ExitApplication();
    }
    private void MinimizeClick(object sender,RoutedEventArgs e)=>SystemCommands.MinimizeWindow(this);
    private void MaximizeClick(object sender,RoutedEventArgs e){if(WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(this);else SystemCommands.MaximizeWindow(this);}
    private void CloseClick(object sender,RoutedEventArgs e)=>Close();
    private void ExitClick(object sender, RoutedEventArgs e) => ((App)Application.Current).ExitApplication();
    public void Reveal()
    {
        Show();
        if(WindowState==WindowState.Minimized)WindowState = vm.Settings.WindowMaximized?WindowState.Maximized:WindowState.Normal;
        Activate();
    }
}
