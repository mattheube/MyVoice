using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.App.Services;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public sealed record CollectionVoiceCard(LibraryEntry Entry,string Name,string? Cover,string Kind);
public partial class MainViewModel
{
    public ObservableCollection<LibraryCollection> Collections {get;}=[];
    public ObservableCollection<CollectionVoiceCard> CollectionVoices {get;}=[];
    [ObservableProperty] private LibraryCollection? selectedCollection;
    public bool HasCollection=>SelectedCollection!=null;
    public bool HasEditableCollection=>SelectedCollection is {Id:not "liked"};
    private void InitializeLibraryCollections()
    {
        Collections.Add(new(){Id="liked",Name="Liked"});foreach(var c in Product.Collections)Collections.Add(c);
        foreach(var sound in SoundCards)sound.IsLiked=Product.FavoriteSounds.Contains(sound.Item.Id);
    }
    partial void OnSelectedCollectionChanged(LibraryCollection? value){if(value!=null)SelectedFolder=null;SoundView?.Refresh();RefreshCollectionVoices();OnPropertyChanged(nameof(HasCollection));OnPropertyChanged(nameof(HasEditableCollection));}
    private bool SoundInCollection(SoundCard card)=>SelectedCollection==null||ContainsEntry(SelectedCollection,new("sound",card.Item.Id.ToString()));
    public bool EntryLiked(LibraryEntry entry)=>entry.Kind switch
    {
        "sound"=>Guid.TryParse(entry.Id,out var id)&&Product.FavoriteSounds.Contains(id),
        "ai-voice"=>Product.FavoriteAiVoices.Contains(entry.Id),
        "voice-preset"=>Product.FavoriteDesignedVoices.Contains(entry.Id),
        "classic-voice"=>Settings.FavoriteVoices.Contains(entry.Id),_=>false
    };
    public bool ContainsEntry(LibraryCollection collection,LibraryEntry entry)=>collection.Id=="liked"?EntryLiked(entry):collection.Items.Contains(entry);
    public void ToggleEntryLike(LibraryEntry entry)
    {
        if(entry.Kind=="sound"&&Guid.TryParse(entry.Id,out var id)){if(!Product.FavoriteSounds.Remove(id))Product.FavoriteSounds.Add(id);var sound=SoundCards.FirstOrDefault(s=>s.Item.Id==id);if(sound!=null)sound.IsLiked=Product.FavoriteSounds.Contains(id);}
        else{var list=entry.Kind switch{"ai-voice"=>Product.FavoriteAiVoices,"voice-preset"=>Product.FavoriteDesignedVoices,_=>Settings.FavoriteVoices};if(!list.Remove(entry.Id))list.Add(entry.Id);}
        SaveProduct();ScheduleSave();SoundView.Refresh();PresetView.Refresh();RefreshCollectionVoices();
    }
    public void ToggleCollectionEntry(LibraryCollection collection,LibraryEntry entry)
    {
        if(collection.Id=="liked"){ToggleEntryLike(entry);return;}
        if(!collection.Items.Remove(entry))collection.Items.Add(entry);
        SaveProduct();SoundView.Refresh();RefreshCollectionVoices();
    }
    public void CreateCollectionFor(LibraryEntry? entry=null)
    {
        var name=Dialogs.Prompt("Nouvelle collection");if(string.IsNullOrWhiteSpace(name))return;
        var collection=new LibraryCollection{Name=name[..Math.Min(name.Length,60)]};if(entry!=null)collection.Items.Add(entry);
        Product.Collections.Add(collection);Collections.Add(collection);SaveProduct();SelectedCollection=collection;
    }
    [RelayCommand] private void CreateCollection()=>CreateCollectionFor();
    [RelayCommand] private void ShowLibrary(){SelectedCollection=null;SelectedFolder=null;FavoriteSoundsOnly=false;SoundView.Refresh();}
    [RelayCommand] private void ShowLiked(){FavoriteSoundsOnly=false;SelectedCollection=Collections.First(c=>c.Id=="liked");}
    [RelayCommand] private void RenameCollection()
    {
        if(SelectedCollection is not {} c||c.Id=="liked")return;var name=Dialogs.Prompt("Renommer la collection",c.Name);if(name==null)return;c.Name=name[..Math.Min(name.Length,60)];
        var index=Collections.IndexOf(c);Collections.RemoveAt(index);Collections.Insert(index,c);SelectedCollection=c;SaveProduct();
    }
    [RelayCommand] private void DeleteCollection()
    {
        if(SelectedCollection is not {} c||c.Id=="liked"||!Dialogs.Confirm(T["Runtime104"]))return;
        Product.Collections.Remove(c);Collections.Remove(c);SelectedCollection=null;SaveProduct();
    }
    public void RefreshCollectionVoices()
    {
        CollectionVoices.Clear();if(SelectedCollection==null)return;
        foreach(var v in AiVoices){var e=new LibraryEntry("ai-voice",v.Id);if(ContainsEntry(SelectedCollection,e))CollectionVoices.Add(new(e,v.Name,v.Cover,"Voice IA"));}
        foreach(var v in DesignedVoices){var e=new LibraryEntry("voice-preset",v.Id);if(ContainsEntry(SelectedCollection,e))CollectionVoices.Add(new(e,v.Name,null,"Preset"));}
        foreach(var v in Presets){var e=new LibraryEntry("classic-voice",v.Name);if(ContainsEntry(SelectedCollection,e))CollectionVoices.Add(new(e,v.DisplayName,null,"Voice"));}
    }
    [RelayCommand] private void ActivateCollectionVoice(CollectionVoiceCard? card)
    {
        if(card==null)return;var e=card.Entry;
        if(e.Kind=="ai-voice")_=UseAiVoiceCommand.ExecuteAsync(AiVoices.FirstOrDefault(v=>v.Id==e.Id));
        else if(e.Kind=="voice-preset")UseDesignedVoice(DesignedVoices.FirstOrDefault(v=>v.Id==e.Id));
        else SelectPreset(Presets.FirstOrDefault(v=>v.Name==e.Id));
    }
    [RelayCommand] private void PreviewSound(SoundCard? card)
    {
        if(card==null)return;
        if(graph.MonitorId==null&&enableOutputs){ShowToast(T["Runtime18"]);return;}
        try{SelectedSound=card;sounds.Preview(card.Item.Id);NowPlayingVisible=true;ShowToast(T["Runtime78"]);}catch(Exception e){StudioError(e.Message);}
    }
    public void OpenSoundInspector(SoundCard card){SelectedSound=card;NowPlayingVisible=true;}
    public void AssignSoundKeybind(SoundCard card){SelectedSound=card;HotkeyTarget="Selected sound";Page=3;ShowToast("Cliquez sur Associer puis appuyez sur votre raccourci.");}
}
