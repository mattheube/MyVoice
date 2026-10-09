using System.Windows;
using System.Windows.Controls;
using MyVoice.App.ViewModels;
using MyVoice.Infrastructure;
namespace MyVoice.App.Views;
public partial class ProfileView:UserControl
{
 public ProfileView(){InitializeComponent();}
 private void PasswordChanged(object sender,RoutedEventArgs e){if(Strength==null||Confirmation==null)return;var score=AccountRules.PasswordScore(Password.Password);Strength.Value=score;StrengthText.Text=new[]{"Faible","Correct","Bon","Fort"}[score];MatchText.Text=Confirmation.Password.Length==0?"":Confirmation.Password==Password.Password?"Les mots de passe correspondent ✓":"Les mots de passe ne correspondent pas";}
 private async void Register(object sender,RoutedEventArgs e){await ((MainViewModel)DataContext).AccountAction("register",Email.Text,Username.Text,DisplayName.Text,Password.Password,Confirmation.Password);Password.Clear();Confirmation.Clear();}
 private async void Login(object sender,RoutedEventArgs e){await ((MainViewModel)DataContext).AccountAction("login",Email.Text,"","",Password.Password,"");Password.Clear();Confirmation.Clear();}
 private async void Recover(object sender,RoutedEventArgs e)=>await ((MainViewModel)DataContext).AccountAction("recover",Email.Text,"","","","");
}
