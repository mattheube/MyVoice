using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Soundboard;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private List<OfficialPack>? officialSoundPacks;
    public List<OfficialPack> OfficialSoundPacks {
        get{
            if(officialSoundPacks!=null)return officialSoundPacks;
            try{officialSoundPacks=OfficialPacks.Load(Path.Combine(AppContext.BaseDirectory,"OfficialPacks"));foreach(var pack in officialSoundPacks)pack.Cover=Services.OfficialPackArtwork.Create(pack.Id,Path.Combine(store.Root,"images","official-packs"));}
            catch(Exception e){store.Log("Official packs: "+e.Message);officialSoundPacks=[];}
            return officialSoundPacks;
        }
    }
    [RelayCommand] private void OpenOfficialPack(OfficialPack? pack){
        if(pack==null)return;
        try{
            var folder=OfficialPacks.Install(pack,Path.Combine(AppContext.BaseDirectory,"OfficialPacks"),store.Root,sounds.Library);sounds.Save();
            if(!Folders.Contains(folder))Folders.Add(folder);
            var present=SoundCards.Select(s=>s.Item.Id).ToHashSet();foreach(var item in sounds.Library.Sounds.Where(s=>!present.Contains(s.Id)))SoundCards.Add(new(item));
            SelectedCollection=null;FavoriteSoundsOnly=false;Search="";SelectedFolder=folder;Page=1;
        }catch(Exception e){ShowToast(e.Message);}
    }
}
