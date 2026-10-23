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
        if(SelectedFolder==null){ShowToast(T["Runtime107"]);return;}
        var dialog=new SaveFileDialog{Filter="MyVoice package|*.myvoice",FileName="Soundboard.myvoice"};if(dialog.ShowDialog()!=true)return;
        var staging=Path.Combine(store.Root,"temp",Guid.NewGuid()+".zip");
        try{var folder=SelectedFolder;var items=sounds.Library.Sounds.Where(s=>s.Folder==folder.Name).ToArray();await Task.Run(()=>CreationPackage.ExportBoard(staging,folder,items));File.Copy(staging,dialog.FileName,true);ShowToast(T["Runtime65"]);}catch(Exception e){StudioError(e.Message);}finally{if(File.Exists(staging))File.Delete(staging);}
    }
    [RelayCommand] private async Task ExportPresetAsync(DesignedVoice? preset)
    {
        if(preset==null)return;
        var dialog=new SaveFileDialog{Filter="MyVoice package|*.myvoice",FileName="VoicePreset.myvoice"};if(dialog.ShowDialog()!=true)return;
        var staging=Path.Combine(store.Root,"temp",Guid.NewGuid()+".zip");
        try{Directory.CreateDirectory(Path.GetDirectoryName(staging)!);await Task.Run(()=>CreationPackage.ExportPreset(staging,preset));File.Copy(staging,dialog.FileName,true);ShowToast(T["Runtime67"]);}catch(Exception e){StudioError(e.Message);}finally{if(File.Exists(staging))File.Delete(staging);}
    }
    [RelayCommand] private async Task ImportPackageAsync()
    {
        var dialog=new OpenFileDialog{Filter="MyVoice package|*.myvoice;*.zip"};if(dialog.ShowDialog()!=true)return;
        try{await InstallCreation(dialog.FileName);ShowToast(T["Runtime34"]);}catch(Exception e){StudioError(e.Message);}
    }
    private async Task InstallCreation(string path,string? sourceId=null,CommunityCard? presentation=null)
    {
        var result=await Task.Run(()=>CreationPackage.Extract(path,Path.Combine(store.Root,"packages")));var m=result.Manifest;
        string title=presentation?.Title??m.Title;string? currentCover=null;
        if(presentation?.Cover is {} cover&&File.Exists(cover)){currentCover=Path.Combine(result.Directory,"community-cover"+Path.GetExtension(cover));File.Copy(cover,currentCover,true);}
        if(m.Kind=="ai-voice"){
            var voice=m.AiVoice!;voice.Id=Guid.NewGuid().ToString("N");voice.SourceContentId=sourceId??"imported-package";
            if(presentation!=null)voice.Name=title;
            voice.ReferenceFile=Path.Combine(result.Directory,voice.ReferenceFile);if(voice.Cover!=null)voice.Cover=Path.Combine(result.Directory,voice.Cover);
            voice.Cover=currentCover??voice.Cover;
            using(var reader=new NAudio.Wave.AudioFileReader(voice.ReferenceFile)){if(reader.TotalTime.TotalSeconds<=0)throw new InvalidDataException(T["Runtime92"]);voice.Duration=reader.TotalTime.TotalSeconds;}
            foreach(var s in voice.Samples){s.OriginalFile=Path.Combine(result.Directory,s.OriginalFile);s.Segments=s.Segments.Select(x=>x with{File=Path.Combine(result.Directory,x.File)}).ToList();}
            AiVoices.Add(voice);SaveVoices();return;
        }
        if(m.Kind=="voice-preset")
        {var preset=m.Preset! with{Id=Guid.NewGuid().ToString("N"),SourceContentId=sourceId??"imported-package",Name=presentation!=null?title:m.Preset!.Name};Product.Presets.Add(preset);DesignedVoices.Add(preset);SaveProduct();return;}
        // Validate all audio before touching the catalog. Keep the installed package for offline use.
        foreach(var s in m.Sounds){using var reader=new NAudio.Wave.AudioFileReader(Path.Combine(result.Directory,s.File));if(reader.TotalTime.TotalSeconds<=0)throw new InvalidDataException("Son vide.");}
        var name=title;int suffix=2;while(Folders.Any(f=>f.Name==name))name=title+" ("+suffix+++ ")";
        var folder=new SoundFolder{Name=name,Cover=currentCover??(m.Cover==null?null:Path.Combine(result.Directory,m.Cover))};
        sounds.Library.Folders.Add(folder);Folders.Add(folder);
        foreach(var s in m.Sounds){var item=new SoundItem{SourceContentId=sourceId??"imported-package",Name=s.Name,File=Path.Combine(result.Directory,s.File),Cover=s.Cover==null?null:Path.Combine(result.Directory,s.Cover),Folder=name,Volume=s.Volume,Loop=s.Loop,BassDb=s.BassDb,Saturation=s.Saturation,Description=s.Description,Category=s.Category,Tags=s.Tags?.ToList()??[]};sounds.Library.Sounds.Add(item);SoundCards.Add(new(item));}
        sounds.Save();SelectedFolder=folder;Page=1;
    }
}
