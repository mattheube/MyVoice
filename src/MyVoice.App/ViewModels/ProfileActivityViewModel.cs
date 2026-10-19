using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    public async Task ShowProfileActivity(string kind)
    {
        if(!SignedIn)return;
        try
        {
            var rows=await community.Rpc("profile_activity",new{list_kind=kind});
            var panel=new StackPanel{Margin=new Thickness(24)};
            var heading=T[kind=="followers"?"ProfileFollowers":kind=="following"?"ProfileFollowing":"ProfileLikes"];
            panel.Children.Add(new TextBlock{Text=heading,FontSize=24,Margin=new Thickness(0,0,0,12)});
            panel.Children.Add(new TextBlock{Text=T[rows.GetArrayLength()==0?"ProfileListEmpty":"ProfileListLimit"],TextWrapping=TextWrapping.Wrap});
            foreach(var row in rows.EnumerateArray())
            {
                var username=row.GetProperty("username").GetString()!;
                var name=row.GetProperty("name").GetString();var detail=row.GetProperty("detail").GetString();
                panel.Children.Add(new TextBlock{Text=name+" · @"+username+(string.IsNullOrEmpty(detail)?"":"\n♥ "+detail),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)});
            }
            var window=new Window{Title=heading,Owner=Application.Current.MainWindow,Icon=Application.Current.MainWindow?.Icon,Width=480,Height=540,Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},WindowStartupLocation=WindowStartupLocation.CenterOwner};
            window.SetResourceReference(Window.BackgroundProperty,"SurfaceBrush");window.SetResourceReference(Window.ForegroundProperty,"TextBrush");window.ShowDialog();
        }
        catch(Exception error){AccountStatus=error.Message;ShowToast(error.Message);}
    }
}
