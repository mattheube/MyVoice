using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyVoice.App.ViewModels;
namespace MyVoice.App.Views;
public partial class CreationCard : UserControl
{
    private ScrollViewer? scroll;
    public CreationCard(){InitializeComponent();Loaded+=OnLoaded;Unloaded+=OnUnloaded;DataContextChanged+=(_,_)=>CheckVisible();SizeChanged+=(_,_)=>CheckVisible();}
    private void OnLoaded(object sender,RoutedEventArgs e){
        DependencyObject? parent=this;while(parent!=null){parent=VisualTreeHelper.GetParent(parent);if(parent is ScrollViewer found){scroll=found;break;}}
        if(scroll!=null)scroll.ScrollChanged+=OnScroll;CheckVisible();
    }
    private void OnUnloaded(object sender,RoutedEventArgs e){if(scroll!=null)scroll.ScrollChanged-=OnScroll;scroll=null;}
    private void OnScroll(object sender,ScrollChangedEventArgs e)=>CheckVisible();
    private void CheckVisible(){
        if(!IsLoaded||!IsVisible||ActualHeight<=0||DataContext is not CommunityCard card||card.CoverRequested||Model is not {} vm)return;
        if(scroll!=null){var bounds=TransformToAncestor(scroll).TransformBounds(new Rect(0,0,ActualWidth,ActualHeight));if(!bounds.IntersectsWith(new Rect(0,0,scroll.ActualWidth,scroll.ActualHeight)))return;}
        _=vm.LoadCommunityCover(card);
    }
    private async void RetryCover(object sender,RoutedEventArgs e){if(DataContext is CommunityCard card&&Model is {} vm)await vm.LoadCommunityCover(card,true);}
    private MainViewModel? Model=>Window.GetWindow(this)?.DataContext as MainViewModel;
    private async void Preview(object sender,RoutedEventArgs e){if(DataContext is CommunityCard card&&Model is {} vm)await vm.PreviewCommunity(card);}
    private async void Install(object sender,RoutedEventArgs e){if(DataContext is CommunityCard card&&Model is {} vm){vm.SelectedCreation=card;await vm.InstallCommunityCreationCommand.ExecuteAsync(null);}}
    private void OpenMenu(object sender,ContextMenuEventArgs e)
    {
        if(DataContext is not CommunityCard card||Model is not {} vm)return;
        var menu=new ContextMenu();
        void Add(string label,System.Action action){var item=new MenuItem{Header=label};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Add(vm.T["CardPreview"],()=>_=vm.PreviewCommunity(card));
        var collections=new MenuItem{Header=vm.T["CardCollection"]};
        foreach(var collection in vm.Collections){if(collection.Id=="liked")continue;var entry=new MenuItem{Header=collection.Name};entry.Click+=async(_,_)=>await vm.AddCommunityCollection(card,collection);collections.Items.Add(entry);}
        var create=new MenuItem{Header=vm.T["NewCollection"]};create.Click+=async(_,_)=>await vm.AddCommunityCollection(card);collections.Items.Add(create);menu.Items.Add(collections);
        Add(vm.T["CardCreator"],()=>_=vm.ViewCommunityCreator(card));
        Add(vm.T["CardDetails"],()=>vm.InspectCommunity(card));
        if(vm.OwnsCreation(card))Add(vm.T["PublicationEditTitle"],()=>_=vm.EditPublication(card));
        Add(card.Liked?vm.T["CardUnlike"]:vm.T["CardLike"],()=>{vm.SelectedCreation=card;vm.LikeCreationCommand.Execute(null);});
        Add(vm.T["CardReport"],()=>{vm.SelectedCreation=card;vm.ReportCreationCommand.Execute(null);});
        ((Button)sender).ContextMenu=menu;
    }
}
