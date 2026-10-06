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
    public static bool Confirm(string message) => MessageBox.Show(Application.Current.MainWindow, message, "MyVoice", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
