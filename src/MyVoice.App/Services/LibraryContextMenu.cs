using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MyVoice.App.ViewModels;
using MyVoice.Core;
namespace MyVoice.App.Services;
public static class LibraryContextMenu
{
    public static ContextMenu Build(MainViewModel vm,object item)
    {
        var menu=new ContextMenu();
        LibraryEntry? entry=item switch{SoundCard c=>new("sound",c.Item.Id.ToString()),VoiceReference v=>new("ai-voice",v.Id),DesignedVoice v=>new("voice-preset",v.Id),PresetCard v=>new("classic-voice",v.Name),CollectionVoiceCard v=>v.Entry,_=>null};
        if(entry==null)return menu;
        MenuItem Add(string label,System.Action action,bool enabled=true,string icon="voice"){var m=new MenuItem{Header=label,IsEnabled=enabled,Icon=new ContextMenuIcon{Kind=icon}};if(icon is "library-remove" or "trash")m.SetResourceReference(MenuItem.ForegroundProperty,"DangerBrush");m.Click+=(_,_)=>action();menu.Items.Add(m);return m;}
        if(item is SoundCard sound)
        {
            Add(vm.T["PreviewLocal"],()=>vm.PreviewSoundCommand.Execute(sound),icon:"headphones");
            Add(vm.T["OpenInspector"],()=>vm.OpenSoundInspector(sound),icon:"inspector");
            Add(vm.T["AddKeybind"],()=>vm.AssignSoundKeybind(sound),icon:"keyboard");
            if(sound.Owned){Add(vm.T["EditInStudio"],()=>vm.EditSoundInStudio(sound),icon:"edit");
            Add(vm.T["PreparePublication"],()=>_=vm.PrepareSoundPublication(sound),icon:"upload");}
        }
        if(item is DesignedVoice preset && string.IsNullOrEmpty(preset.SourceContentId))Add(vm.T["PreparePublication"],()=>_=vm.PreparePresetPublication(preset),icon:"upload");
        Add(vm.EntryLiked(entry)?vm.T["Unlike"]:vm.T["Like"],()=>vm.ToggleEntryLike(entry),icon:vm.EntryLiked(entry)?"heart-filled":"heart");
        var collections=new MenuItem{Header=vm.T["AddToCollection"],Icon=new ContextMenuIcon{Kind="collection"}};menu.Items.Add(collections);
        foreach(var c in vm.Collections.Where(c=>c.Id!="liked")){var member=new MenuItem{Header=c.Name,IsCheckable=true,IsChecked=vm.ContainsEntry(c,entry)};member.Click+=(_,_)=>vm.ToggleCollectionEntry(c,entry);collections.Items.Add(member);}
        collections.Items.Add(new Separator());var create=new MenuItem{Header=vm.T["NewCollection"],Icon=new ContextMenuIcon{Kind="collection"}};create.Click+=(_,_)=>vm.CreateCollectionFor(entry);collections.Items.Add(create);
        if(vm.SelectedCollection is {} current&&vm.ContainsEntry(current,entry))Add(vm.T["RemoveFromCollection"],()=>vm.ToggleCollectionEntry(current,entry),icon:"collection-remove");
        if(item is SoundCard card){menu.Items.Add(new Separator());Add(vm.T["RemoveFromLibrary"],()=>{vm.SelectedSound=card;vm.DeleteSoundCommand.Execute(null);},icon:"library-remove");}
        return menu;
    }
}
