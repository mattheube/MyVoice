namespace MyVoice.App.Views;
public partial class VoiceLabView : System.Windows.Controls.UserControl
{
    private void InsertTextControl(object sender,System.Windows.RoutedEventArgs e)
    {
        if(sender is not System.Windows.Controls.Button {Tag:string marker})return;
        if(VoiceTextEditor.Text.Length-VoiceTextEditor.SelectionLength+marker.Length>VoiceTextEditor.MaxLength)return;
        var start=VoiceTextEditor.SelectionStart;
        VoiceTextEditor.SelectedText=marker;
        VoiceTextEditor.CaretIndex=start+marker.Length;
        VoiceTextEditor.Focus();
    }
    private void AudioDragOver(object sender,System.Windows.DragEventArgs e){e.Effects=e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)?System.Windows.DragDropEffects.Copy:System.Windows.DragDropEffects.None;e.Handled=true;}
    private async void AudioDrop(object sender,System.Windows.DragEventArgs e){e.Handled=true;if(e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files&&DataContext is MyVoice.App.ViewModels.MainViewModel vm)await vm.DropVoiceSamples(files);}
    private async void ConversionDrop(object sender,System.Windows.DragEventArgs e){e.Handled=true;if(e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files&&DataContext is MyVoice.App.ViewModels.MainViewModel vm){if(files.Length!=1){vm.ShowToast("Select one audio file for conversion.");return;}await vm.SetConversionInput(files[0]);}}
    public VoiceLabView()
    {
        InitializeComponent();
    }
}
