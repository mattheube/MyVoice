using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyVoice.App.ViewModels;
namespace MyVoice.App;
public partial class MainWindow
{
    private Window? searchWindow;
    private Window ShellWindow(string title,int height=450)
    {
        var result=new Window{Owner=this,Icon=Icon,Title=title,Width=580,Height=height,MinWidth=400,MinHeight=250,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        result.SetResourceReference(BackgroundProperty,"CanvasBrush");result.SetResourceReference(ForegroundProperty,"TextBrush");
        result.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)result.Close();};return result;
    }
    private void SearchClick(object sender,RoutedEventArgs e)=>ShowSearch();
    private void ShowSearch()
    {
        if(searchWindow!=null){searchWindow.Activate();return;}
        var dialog=searchWindow=ShellWindow(vm.T["SearchEverywhere"]);
        var panel=new DockPanel{Margin=new Thickness(20)};var query=new TextBox{Margin=new Thickness(0,0,0,16)};
        DockPanel.SetDock(query,Dock.Top);panel.Children.Add(query);
        var results=new ListBox{DisplayMemberPath="Title"};panel.Children.Add(results);dialog.Content=panel;
        void Refresh(){results.ItemsSource=vm.FindDestinations(query.Text).ToArray();if(results.Items.Count>0)results.SelectedIndex=0;}
        void Open(){if(results.SelectedItem is SearchDestination target){dialog.Close();target.Open();}}
        query.TextChanged+=(_,_)=>Refresh();query.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Down){results.Focus();e.Handled=true;}if(e.Key==Key.Enter){Open();e.Handled=true;}};
        results.MouseDoubleClick+=(_,_)=>Open();results.KeyDown+=(_,e)=>{if(e.Key==Key.Enter)Open();};
        dialog.Loaded+=(_,_)=>query.Focus();dialog.Closed+=(_,_)=>searchWindow=null;Refresh();dialog.Show();
    }
    private void NotificationsClick(object sender,RoutedEventArgs e)
    {
        var dialog=ShellWindow(vm.T["Notifications"]);var list=new StackPanel{Margin=new Thickness(20)};
        var panel=new DockPanel{Margin=new Thickness(16)};var bar=new WrapPanel();DockPanel.SetDock(bar,Dock.Top);panel.Children.Add(bar);
        var unread=new CheckBox{Content=vm.T["UnreadOnly"]};bar.Children.Add(unread);
        var category=new ComboBox{Width=150,ItemsSource=new[]{"all","application","updates","community"},SelectedIndex=0};bar.Children.Add(category);
        var mark=new Button{Content=vm.T["MarkAllRead"]};bar.Children.Add(mark);
        void Refresh()
        {
            list.Children.Clear();
            var selected=vm.Product.Notifications.Where(n=>(unread.IsChecked!=true||!n.Read)&&(category.SelectedItem?.ToString()=="all"||n.Category==category.SelectedItem?.ToString())).ToArray();
            if(selected.Length==0)list.Children.Add(new TextBlock{Text=vm.T["NoNotifications"]});
            foreach(var n in selected)
            {
                var row=new StackPanel{Margin=new Thickness(0,0,0,18)};
                row.Children.Add(new TextBlock{Text=n.CreatedAt.ToLocalTime().ToString("g")+" · "+n.Category,Opacity=.65});
                row.Children.Add(new TextBlock{Text=n.Text,TextWrapping=TextWrapping.Wrap,FontWeight=n.Read?FontWeights.Normal:FontWeights.SemiBold});list.Children.Add(row);
            }
        }
        unread.Checked+=(_,_)=>Refresh();unread.Unchecked+=(_,_)=>Refresh();category.SelectionChanged+=(_,_)=>Refresh();mark.Click+=(_,_)=>{vm.MarkNotificationsRead();Refresh();};
        panel.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});dialog.Content=panel;Refresh();dialog.Show();
    }
    private void AccountMenuClick(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu();void Add(string label,Action action){var item=new MenuItem{Header=label};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Add(vm.T["Account"],()=>vm.Page=7);
        Add(vm.T["Help"],()=>vm.Page=6);
        Add("Discord",()=>{try{Process.Start(new ProcessStartInfo("https://discord.gg/SDhAJgvxzq"){UseShellExecute=true});}catch(Exception error){vm.ShowToast(error.Message);}});
        Add(vm.T["About"],()=>MessageBox.Show(this,"MyVoice\n"+typeof(MainWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().First().InformationalVersion,"MyVoice"));
        menu.Items.Add(new Separator());Add(vm.T["Quit"],()=>((App)Application.Current).ExitApplication());
        menu.PlacementTarget=(Button)sender;menu.IsOpen=true;
    }
}
