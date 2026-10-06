using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MyVoice.App.ViewModels;
namespace MyVoice.App.Views;
public partial class SettingsView : UserControl
{
    private bool capturing;
    public SettingsView()
    {
        InitializeComponent();
    }
    private void BeginCapture(object sender, RoutedEventArgs e)
    {
        capturing = true;
        CaptureButton.Content = ((MainViewModel)DataContext).T["Press"];
        CaptureButton.Focus();
    }
    private void CancelCapture(object sender, KeyboardFocusChangedEventArgs e) => Reset();
    private void Reset()
    {
        capturing = false;
        CaptureButton.SetBinding(ContentControl.ContentProperty, new Binding("T[Bind]"));
    }
    private void CaptureKey(object sender, KeyEventArgs e)
    {
        if (!capturing)
            return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            Reset();
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;
        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None || (mods & ModifierKeys.Windows) != 0)
            return;
        uint value = (uint)(((mods & ModifierKeys.Alt) != 0 ? 1 : 0) | ((mods & ModifierKeys.Control) != 0 ? 2 : 0) | ((mods & ModifierKeys.Shift) != 0 ? 4 : 0));
        ((MainViewModel)DataContext).SetHotkey(value, (uint)KeyInterop.VirtualKeyFromKey(key));
        Reset();
    }
}
