using System.Windows;
using System.Windows.Controls;
using MyVoice.App.ViewModels;
using MyVoice.App.Services;
namespace MyVoice.App.Views;
public partial class CommunityView:UserControl{
 public CommunityView(){InitializeComponent();Loaded+=async(_,_)=>{if(DataContext is MainViewModel vm&&vm.CommunityItems.Count==0)await vm.RefreshCommunityCommand.ExecuteAsync(null);};}
 private async void Preview(object sender,RoutedEventArgs e){if(sender is FrameworkElement {DataContext:CommunityCard card}&&DataContext is MainViewModel vm)await vm.PreviewCommunity(card);}
 private void OpenMenu(object sender,ContextMenuEventArgs e){if(sender is not FrameworkElement {DataContext:CommunityCard card} element||DataContext is not MainViewModel vm)return;vm.SelectedCreation=card;var menu=new ContextMenu();void Add(string label,System.Action action,string icon){var item=new MenuItem{Header=label,Icon=new ContextMenuIcon{Kind=icon}};item.Click+=(_,_)=>action();menu.Items.Add(item);}Add("Preview",()=>_=vm.PreviewCommunity(card),"headphones");Add("Open Inspector",()=>vm.InspectCommunity(card),"inspector");Add(card.Liked?"Unlike":"Like",()=>vm.LikeCreationCommand.Execute(null),"heart");var collections=new MenuItem{Header="Add to Collection",Icon=new ContextMenuIcon{Kind="collection"}};foreach(var collection in vm.Collections){if(collection.Id=="liked")continue;var item=new MenuItem{Header=collection.Name};item.Click+=async(_,_)=>await vm.AddCommunityCollection(card,collection);collections.Items.Add(item);}var create=new MenuItem{Header="Nouvelle collection"};create.Click+=async(_,_)=>await vm.AddCommunityCollection(card);collections.Items.Add(create);menu.Items.Add(collections);Add("Add to Library",()=>vm.InstallCommunityCreationCommand.Execute(null),"upload");Add("View Creator",()=>_=vm.ViewCommunityCreator(card),"profile");Add("Report",()=>vm.ReportCreationCommand.Execute(null),"flag");element.ContextMenu=menu;}
}
