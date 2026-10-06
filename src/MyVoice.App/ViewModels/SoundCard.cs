using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public sealed class SoundCard : ObservableObject
{
    public SoundItem Item
    {
        get;
    }
    public SoundCard(SoundItem item)
    {
        Item = item;
        try { if(System.IO.File.Exists(item.File)) { using var reader=new NAudio.Wave.AudioFileReader(item.File); Duration=reader.TotalTime.ToString(@"mm\:ss"); } else Duration="File missing"; } catch { Duration="Unavailable"; }
    }
    public string Duration { get; private set; } = "—";
    public string Name => Item.Name;
    public string Folder => Item.Folder;
    public string? Cover => Item.Cover;
    public string Initial => string.IsNullOrEmpty(Name) ? "♪" : Name[..1].ToUpperInvariant();
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Folder));
        OnPropertyChanged(nameof(Cover));
        OnPropertyChanged(nameof(Initial));
    }
}
public sealed class PresetCard : ObservableObject
{
    public string Name {get;}
    public string DisplayName=>Name=="Clean"?"Default Clean":Name;
    public string Category {get;}
    public string Code {get;}
    public string Description {get;}
    public PresetCard(string name,string category,string code,string description) { Name=name;Category=category;Code=code;Description=description; }
    private bool selected;
    public bool Selected {get=>selected;set=>SetProperty(ref selected,value);}
    public string Icon => Name switch
    {
      "Robot"=>"M 7,10 L 29,10 29,28 7,28 Z M 18,10 L 18,5 M 13,17 L 13,20 M 23,17 L 23,20 M 12,24 L 24,24",
      "Radio"=>"M 5,13 L 31,13 31,29 5,29 Z M 9,13 L 26,5 M 9,19 L 20,19 M 9,23 L 20,23 M 26,19 L 26,24",
      "Deep"=>"M 6,12 L 6,20 M 12,7 L 12,25 M 18,10 L 18,30 M 24,15 L 24,25 M 30,18 L 30,22",
      "High"=>"M 6,18 L 6,22 M 12,14 L 12,26 M 18,6 L 18,30 M 24,10 L 24,25 M 30,14 L 30,20",
      "Echo"=>"M 6,12 Q 18,18 6,24 M 14,8 Q 30,18 14,28 M 22,4 Q 40,18 22,32",
      "Megaphone"=>"M 5,15 L 27,6 27,30 5,21 Z M 10,23 L 14,32 19,32 16,25",
      "Walkie Talkie"=>"M 10,10 L 26,10 26,31 10,31 Z M 13,10 L 13,2 M 14,16 L 22,16 M 14,21 L 22,21 M 14,25 L 22,25",
      "Demon"=>"M 8,13 L 5,4 15,10 M 28,13 L 31,4 21,10 M 7,14 Q 18,4 29,14 L 26,27 18,32 10,27 Z M 12,18 L 15,21 M 24,18 L 21,21",
      "Tiny"=>"M 11,19 Q 11,9 18,9 Q 25,9 25,19 Q 25,27 18,27 Q 11,27 11,19 M 18,3 L 18,6 M 6,12 L 9,14 M 30,12 L 27,14",
      _=>"M 13,7 Q 18,2 23,7 L 23,19 Q 18,24 13,19 Z M 8,16 L 8,20 Q 18,33 28,20 L 28,16 M 18,28 L 18,33 M 12,33 L 24,33"
    };
}
