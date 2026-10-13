using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace MyVoice.App.ViewModels;
public sealed record SoundRow(IReadOnlyList<SoundCard> Sounds);
public partial class MainViewModel
{
    public ObservableCollection<SoundRow> SoundRows {get;}=[];
    private int soundColumns=4;
    public void SetSoundColumns(int columns){columns=Math.Clamp(columns,1,12);if(columns==soundColumns)return;soundColumns=columns;RefreshSoundRows();}
    private void RefreshSoundRows()
    {
        if(SoundView==null)return;
        var cards=SoundView.Cast<SoundCard>().ToArray();SoundRows.Clear();
        foreach(var row in cards.Chunk(soundColumns))SoundRows.Add(new(row));
        OnPropertyChanged(nameof(VisibleSoundCount));
    }
    public string VisibleSoundCount=>SoundView==null?"":SoundView.Cast<object>().Count()+" "+T["SoundsCount"];
}
