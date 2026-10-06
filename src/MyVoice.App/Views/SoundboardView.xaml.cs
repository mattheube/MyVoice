using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyVoice.App.ViewModels;
namespace MyVoice.App.Views;
public partial class SoundboardView : UserControl
{
    public SoundboardView()
    {
        InitializeComponent();
    }
    private async void FilesDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await ((MainViewModel)DataContext).ImportPathsAsync(paths);
    }
    private void SelectCard(object sender, MouseButtonEventArgs e)
    { if(sender is FrameworkElement {DataContext:SoundCard card}) ((MainViewModel)DataContext).SelectSoundCommand.Execute(card); }
    private void HoldStart(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button { DataContext: SoundCard card } button && card.Item.PlaybackMode == "Hold")
        {
            ((MainViewModel)DataContext).PlaySoundCommand.Execute(card);
            button.CaptureMouse();
            e.Handled = true;
        }
    }
    private void HoldStop(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button { DataContext: SoundCard card } button && card.Item.PlaybackMode == "Hold")
        {
            ((MainViewModel)DataContext).ReleaseSound(card);
            button.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
    private void HoldLost(object sender, MouseEventArgs e)
    {
        if (sender is Button { DataContext: SoundCard card } && card.Item.PlaybackMode == "Hold")
            ((MainViewModel)DataContext).ReleaseSound(card);
    }
}
