namespace MyVoice.App.Views;
public partial class VoiceLabView : System.Windows.Controls.UserControl
{
    private void AudioDragOver(object sender,System.Windows.DragEventArgs e){e.Effects=e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)?System.Windows.DragDropEffects.Copy:System.Windows.DragDropEffects.None;e.Handled=true;}
    private async void AudioDrop(object sender,System.Windows.DragEventArgs e){e.Handled=true;if(e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files&&DataContext is MyVoice.App.ViewModels.MainViewModel vm)await vm.DropVoiceSamples(files);}
    private async void ConversionDrop(object sender,System.Windows.DragEventArgs e){e.Handled=true;if(e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files&&DataContext is MyVoice.App.ViewModels.MainViewModel vm){if(files.Length!=1){vm.ShowToast("Select one audio file for conversion.");return;}await vm.SetConversionInput(files[0]);}}
    public VoiceLabView()
    {
        InitializeComponent();
    }
}
