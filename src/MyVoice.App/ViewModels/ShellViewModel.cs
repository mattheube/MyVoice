using System;
using System.Collections.Generic;
using System.Linq;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public sealed record SearchDestination(string Title,string Detail,Action Open);
public partial class MainViewModel
{
    public string AccountLabel=>SignedIn?(string.IsNullOrWhiteSpace(ProfileName)?T["Account"]:ProfileName):T["Guest"];
    public string ProfileFirstName {get=>Product.FirstName;set{Product.FirstName=value.Trim();SaveProduct();OnPropertyChanged();}}
    public string ProfileLastName {get=>Product.LastName;set{Product.LastName=value.Trim();SaveProduct();OnPropertyChanged();}}
    public int UnreadNotifications=>Product.Notifications.Count(n=>!n.Read);
    public string NotificationLabel=>$"{T["Notifications"]} · {UnreadNotifications}";
    public void MarkNotificationsRead(){foreach(var n in Product.Notifications)n.Read=true;SaveProduct();OnPropertyChanged(nameof(NotificationLabel));}
    public void RecordNotification(string text,string category="application")
    {
        if(Product.Notifications.FirstOrDefault()?.Text==text)return;
        Product.Notifications.Insert(0,new(){Text=text,Category=category});
        if(Product.Notifications.Count>200)Product.Notifications.RemoveRange(200,Product.Notifications.Count-200);
        SaveProduct();OnPropertyChanged(nameof(NotificationLabel));
    }
    public IEnumerable<SearchDestination> FindDestinations(string query)
    {
        bool Match(string text)=>text.Contains(query.Trim(),StringComparison.CurrentCultureIgnoreCase);
        foreach(var (name,page) in new[]{(T["Home"],4),(T["Voice"],0),(T["Soundboard"],1),(T["Mixer"],8),(T["Community"],5),(T["Studio"],2),(T["Settings"],3),(T["Profile"],7)})
            if(Match(name))yield return new(name,T["Navigation"],()=>Page=page);
        foreach(var key in new[]{"Updates","Notifications","Language","Hotkeys","Theme","Volume"})
            if(Match(T[key]))yield return new(T[key],T["Settings"],()=>Page=3);
        foreach(var voice in DesignedVoices.Where(v=>Match(v.Name)).Take(20))
            yield return new(voice.Name,T["Studio"],()=>{Page=2;StudioTab=1;});
        foreach(var creation in CommunityItems.Where(v=>Match(v.Title)).Take(20))
            yield return new(creation.Title,T["Community"],()=>{SelectedCreation=creation;Page=5;});
        foreach(var sound in SoundCards.Where(s=>Match(s.Name)||Match(s.Item.Description)||s.Item.Tags.Any(Match)).Take(40))
            yield return new(sound.Name,T["Soundboard"],()=>{ShowLibraryCommand.Execute(null);Search=sound.Name;Page=1;OpenSoundInspector(sound);});
        foreach(var voice in AiVoices.Where(v=>Match(v.Name)).Take(20))
            yield return new(voice.Name,T["Voice"],()=>{SelectedAiVoice=voice;Page=0;});
        foreach(var collection in Collections.Where(c=>Match(c.Name)).Take(20))
            yield return new(collection.Name,T["Collections"],()=>{Search="";SelectedCollection=collection;Page=1;});
    }
}
