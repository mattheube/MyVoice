using System.Windows;
using System.Windows.Controls;
using MyVoice.App.ViewModels;
namespace MyVoice.App.Views;
public partial class AccountSettingsView:UserControl
{
 public AccountSettingsView(){InitializeComponent();Loaded+=async(_,_)=>{if(DataContext is MainViewModel vm && vm.SignedIn)await vm.RefreshPublicProfileCommand.ExecuteAsync(null);};}
 private async void ChangePassword(object sender,RoutedEventArgs e){await ((MainViewModel)DataContext).ChangeAccountPassword(NewPassword.Password,NewConfirmation.Password);NewPassword.Clear();NewConfirmation.Clear();}
 private async void RecoverSignedIn(object sender,RoutedEventArgs e){var vm=(MainViewModel)DataContext;await vm.AccountAction("recover",vm.ProfileEmail,"","","","");}
}
