using System.Windows;
using System.Windows.Controls;
using MyVoice.App.ViewModels;
namespace MyVoice.App.Views;
public partial class CreationCard : UserControl
{
    public CreationCard()=>InitializeComponent();
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
        Add(card.Liked?vm.T["CardUnlike"]:vm.T["CardLike"],()=>{vm.SelectedCreation=card;vm.LikeCreationCommand.Execute(null);});
        Add(vm.T["CardReport"],()=>{vm.SelectedCreation=card;vm.ReportCreationCommand.Execute(null);});
        ((Button)sender).ContextMenu=menu;
    }
}
