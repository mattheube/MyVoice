using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MyVoice.App;
public sealed class SplashWindow : Window
{
    private readonly TextBlock status;
    public SplashWindow()
    {
        Width=420; Height=220; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false;
        Background=new SolidColorBrush(Color.FromRgb(28,29,31));
        var panel=new StackPanel { Margin=new Thickness(32) };
        var brand=new StackPanel { Orientation=Orientation.Horizontal };
        var icon=new System.Windows.Media.Imaging.BitmapImage(new System.Uri(System.IO.Path.Combine(System.AppContext.BaseDirectory,"MyVoice.ico")));
        Icon=icon;brand.Children.Add(new Image { Source=icon,Width=42,Height=42,Margin=new Thickness(0,0,14,0) });panel.Children.Add(brand);
        brand.Children.Add(new TextBlock { Text="MyVoice",FontSize=32,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(224,173,98)) });
        status=new TextBlock { Text="Préparation…",Margin=new Thickness(0,24,0,18),Foreground=Brushes.WhiteSmoke };
        panel.Children.Add(status); panel.Children.Add(new ProgressBar { IsIndeterminate=true,Height=3 }); Content=panel;
    }
    public void Stage(string text) => status.Text=text;
}
