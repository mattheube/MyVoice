using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.Core;
using MyVoice.Infrastructure;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    [RelayCommand] private async Task ExportBoardAsync()
    {
        if(SelectedFolder==null){ShowToast("Sélectionnez un Soundboard.");return;}
        var dialog=new SaveFileDialog{Filter="MyVoice package|*.myvoice",FileName="Soundboard.myvoice"};if(dialog.ShowDialog()!=true)return;
        var staging=Path.Combine(store.Root,"temp",Guid.NewGuid()+".zip");
        try{var folder=SelectedFolder;var items=sounds.Library.Sounds.Where(s=>s.Folder==folder.Name).ToArray();await Task.Run(()=>CreationPackage.ExportBoard(staging,folder,items));File.Copy(staging,dialog.FileName,true);ShowToast("Package exporté. Aucun envoi en ligne.");}catch(Exception e){StudioError(e.Message);}finally{if(File.Exists(staging))File.Delete(staging);}
    }
    [RelayCommand] private async Task ExportPresetAsync(DesignedVoice? preset)
    {
        if(preset==null)return;
        var dialog=new SaveFileDialog{Filter="MyVoice package|*.myvoice",FileName="VoicePreset.myvoice"};if(dialog.ShowDialog()!=true)return;
        var staging=Path.Combine(store.Root,"temp",Guid.NewGuid()+".zip");
        try{Directory.CreateDirectory(Path.GetDirectoryName(staging)!);await Task.Run(()=>CreationPackage.ExportPreset(staging,preset));File.Copy(staging,dialog.FileName,true);ShowToast("Preset exporté. Aucun envoi en ligne.");}catch(Exception e){StudioError(e.Message);}finally{if(File.Exists(staging))File.Delete(staging);}
    }
    [RelayCommand] private async Task ImportPackageAsync()
    {
        var dialog=new OpenFileDialog{Filter="MyVoice package|*.myvoice;*.zip"};if(dialog.ShowDialog()!=true)return;
        try{await InstallCreation(dialog.FileName);ShowToast("Création installée dans votre bibliothèque.");}catch(Exception e){StudioError(e.Message);}
    }
    private async Task InstallCreation(string path)
    {
        var result=await Task.Run(()=>CreationPackage.Extract(path,Path.Combine(store.Root,"packages")));var m=result.Manifest;
        if(m.Kind=="voice-preset")
        {var preset=m.Preset! with{Id=Guid.NewGuid().ToString("N")};Product.Presets.Add(preset);DesignedVoices.Add(preset);SaveProduct();return;}
        // Validate all audio before touching the catalog. Keep the installed package for offline use.
        foreach(var s in m.Sounds){using var reader=new NAudio.Wave.AudioFileReader(Path.Combine(result.Directory,s.File));if(reader.TotalTime.TotalSeconds<=0)throw new InvalidDataException("Son vide.");}
        var name=m.Title;int suffix=2;while(Folders.Any(f=>f.Name==name))name=m.Title+" ("+suffix+++ ")";
        var folder=new SoundFolder{Name=name,Cover=m.Cover==null?null:Path.Combine(result.Directory,m.Cover)};
        sounds.Library.Folders.Add(folder);Folders.Add(folder);
        foreach(var s in m.Sounds){var item=new SoundItem{Name=s.Name,File=Path.Combine(result.Directory,s.File),Cover=s.Cover==null?null:Path.Combine(result.Directory,s.Cover),Folder=name,Volume=s.Volume,Loop=s.Loop,BassDb=s.BassDb,Saturation=s.Saturation};sounds.Library.Sounds.Add(item);SoundCards.Add(new(item));}
        sounds.Save();SelectedFolder=folder;Page=1;
    }
}
