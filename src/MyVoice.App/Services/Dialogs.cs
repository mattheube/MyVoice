using System;
using System.Windows;
using System.Windows.Controls;
namespace MyVoice.App.Services;
public static class Dialogs
{
    public static string? Prompt(string title, string initial = "")
    {
        var window = new Window { Icon = Application.Current.MainWindow?.Icon, Title = title, Width = 430, Height = 195, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow, Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#202125")! };
        var panel = new StackPanel { Margin = new Thickness(24) };
        var input = new TextBox { Text = initial, FontSize = 16, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 15) };
        var button = new Button { Content = "OK", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 90 };
        button.Click += (_, _) => window.DialogResult = true;
        panel.Children.Add(input);
        panel.Children.Add(button);
        window.Content = panel;
        window.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return window.ShowDialog() == true && !string.IsNullOrWhiteSpace(input.Text) ? input.Text.Trim() : null;
    }
    public static bool? PublicationClose(string question,string save,string discard,string cancel)
    {
        bool? choice=null;
        var window=new Window{Title="MyVoice",Owner=Application.Current.MainWindow,Icon=Application.Current.MainWindow?.Icon,Width=470,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        window.SetResourceReference(Window.BackgroundProperty,"SurfaceBrush");window.SetResourceReference(Window.ForegroundProperty,"TextBrush");
        var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(new TextBlock{Text=question,FontSize=20,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
        foreach(var item in new[]{(save,(bool?)true),(discard,(bool?)false),(cancel,(bool?)null)}){var button=new Button{Content=item.Item1,IsCancel=item.Item2==null};button.Click+=(_,_)=>{choice=item.Item2;window.Close();};panel.Children.Add(button);}
        window.Content=panel;window.ShowDialog();return choice;
    }
    public static bool Confirm(string message) => MessageBox.Show(Application.Current.MainWindow, message, "MyVoice", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
