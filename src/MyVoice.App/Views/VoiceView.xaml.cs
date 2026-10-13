using System.Windows;
using System.Windows.Controls;
using MyVoice.App.ViewModels;
namespace MyVoice.App.Views;
public partial class VoiceView:UserControl
{
 public VoiceView()=>InitializeComponent();
 private void OpenMenu(object sender,ContextMenuEventArgs e){if(sender is FrameworkElement element&&DataContext is MainViewModel vm)element.ContextMenu=Services.LibraryContextMenu.Build(vm,element.DataContext);}
}
