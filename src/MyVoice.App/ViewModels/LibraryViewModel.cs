using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.Core;
using MyVoice.App.Services;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    [RelayCommand]
    private void CreateFolder()
    {
        var name = Dialogs.Prompt(T["NewFolder"]);
        if (name == null || Folders.Any(f => f.Name == name))
            return;
        var folder = new SoundFolder { Name = name };
        Folders.Add(folder);
        sounds.Library.Folders.Add(folder);
        SelectedFolder = folder;
        sounds.Save();
    }
    [RelayCommand]
    private void RenameFolder()
    {
        if (SelectedFolder == null)
            return;
        var name = Dialogs.Prompt(T["Rename"], SelectedFolder.Name);
        if (name == null || Folders.Any(f => f.Name == name))
            return;
        var old = SelectedFolder.Name;
        SelectedFolder.Name = name;
        foreach (var card in SoundCards.Where(c => c.Folder == old))
        {
            card.Item.Folder = name;
            card.Refresh();
        }
        ReloadFolders();
        sounds.Save();
    }
    [RelayCommand]
    private void DeleteFolder()
    {
        if (SelectedFolder == null || Folders.Count <= 1 || !Dialogs.Confirm(T["DeleteFolderConfirm"]))
            return;
        var old = SelectedFolder;
        var target = Folders.First(f => f != old);
        foreach (var card in SoundCards.Where(c => c.Folder == old.Name))
        {
            card.Item.Folder = target.Name;
            card.Refresh();
        }
        sounds.Library.Folders.Remove(old);
        Folders.Remove(old);
        SelectedFolder = target;
        sounds.Save();
    }
    [RelayCommand]
    private void DuplicateFolder()
    {
        if (SelectedFolder == null)
            return;
        var name = Dialogs.Prompt(T["NewFolder"], SelectedFolder.Name + " copy");
        if (name == null || Folders.Any(f => f.Name == name))
            return;
        var folder = new SoundFolder { Name = name, Cover = SelectedFolder.Cover };
        foreach (var source in SoundCards.Where(c => c.Folder == SelectedFolder.Name).ToArray())
        {
            var copy = new SoundItem { Name = source.Name, File = source.Item.File, Folder = name, Cover = source.Cover, Volume = source.Item.Volume, SourceContentId=source.Item.SourceContentId, Description=source.Item.Description, Category=source.Item.Category, Tags=[..source.Item.Tags], Loop=source.Item.Loop, BassDb=source.Item.BassDb, Saturation=source.Item.Saturation, HearMyself=source.Item.HearMyself, SendToMic=source.Item.SendToMic, PlaybackMode=source.Item.PlaybackMode };
            sounds.Library.Sounds.Add(copy);
            SoundCards.Add(new(copy));
        }
        Folders.Add(folder);
        sounds.Library.Folders.Add(folder);
        SelectedFolder = folder;
        sounds.Save();
    }
    [RelayCommand]
    private void MoveFolderUp()
    {
        if (SelectedFolder == null)
            return;
        int index = Folders.IndexOf(SelectedFolder);
        if (index <= 0)
            return;
        Folders.Move(index, index - 1);
        sounds.Library.Folders = Folders.ToList();
        sounds.Save();
    }
    private void ReloadFolders()
    {
        var selected = SelectedFolder;
        Folders.Clear();
        foreach (var f in sounds.Library.Folders)
            Folders.Add(f);
        SelectedFolder = selected;
        SoundView.Refresh();
    }
    [RelayCommand]
    private void RenameSound()
    {
        if (SelectedSound == null)
            return;
        var name = Dialogs.Prompt(T["Rename"], SelectedSound.Name);
        if (name == null)
            return;
        SelectedSound.Item.Name = name;
        SelectedSound.Refresh();
        sounds.Save();
    }
    [RelayCommand]
    private void DeleteSound()
    {
        if (SelectedSound == null || !Dialogs.Confirm(T["DeleteSoundConfirm"]))
            return;
        var item = SelectedSound;
        sounds.Stop(item.Item.Id);
        sounds.Library.Sounds.Remove(item.Item);
        SoundCards.Remove(item);
        SelectedSound = null;
        sounds.Save();
    }
    public void DropImage(string target,string path){try{MyVoice.Core.MediaValidation.RequireImage(path);if(target=="voice"&&SelectedAiVoice==null||target=="sound"&&SelectedSound==null||target=="folder"&&SelectedFolder==null)return;var cover=ImportCover(target is "voice" or "avatar",path);if(cover==null)return;switch(target){case "publication":SetPublicationCover(cover);break;case "avatar":Product.Avatar=cover;SaveProduct();OnPropertyChanged(nameof(ProfileAvatar));break;case "voice":SelectedAiVoice!.Cover=cover;SaveVoices();ReloadVoices();break;case "sound":SelectedSound!.Item.Cover=cover;SelectedSound.Refresh();sounds.Save();break;case "folder":SelectedFolder!.Cover=cover;sounds.Save();break;}}catch(Exception e){ShowToast(e.Message);}}
    private string? ImportCover(bool circular = false, string? existing = null, double aspectRatio=1)
    {
        string? source = existing;
        if (source != null && File.Exists(source + ".source.txt")) source = File.ReadAllText(source + ".source.txt");
        if (source == null || !File.Exists(source))
        {
            var dialog = new OpenFileDialog { Filter = "Image|*.png;*.jpg;*.jpeg;*.webp;*.gif" };
            if (dialog.ShowDialog() != true) return null;
            source = dialog.FileName;
        }
        MyVoice.Core.MediaValidation.RequireImage(source);
        var crop = new MyVoice.App.ImageCropWindow(source, circular, aspectRatio) { Owner = System.Windows.Application.Current.MainWindow };
        if (crop.ShowDialog() != true || crop.Result == null) return null;
        var id = Guid.NewGuid().ToString("N");
        var dest = crop.SaveExport(Path.Combine(store.Root, "images", id));
        var originals = Path.Combine(store.Root, "images", "originals");
        Directory.CreateDirectory(originals);
        var original = Path.Combine(originals, id + Path.GetExtension(crop.SourceFile));
        File.Copy(crop.SourceFile, original);
        File.WriteAllText(dest + ".source.txt", original);
        return dest;
    }
    [RelayCommand]
    private void ChangeSoundCover()
    {
        if (SelectedSound == null)
            return;
        try
        {
            var path = ImportCover(false, SelectedSound.Cover);
            if (path == null)
                return;
            SelectedSound.Item.Cover = path;
            SelectedSound.Refresh();
            sounds.Save();
        }
        catch (Exception e) { Error(e); }
    }
    [RelayCommand]
    private void ChangeFolderCover()
    {
        if (SelectedFolder == null)
            return;
        try
        {
            var path = ImportCover(false, SelectedFolder.Cover);
            if (path == null)
                return;
            SelectedFolder.Cover = path;
            foreach (var card in SoundCards.Where(c => c.Folder == SelectedFolder.Name && c.Cover == null))
            {
                card.Item.Cover = path;
                card.Refresh();
            }
            sounds.Save();
        }
        catch (Exception e) { Error(e); }
    }
    public bool Gate
    {
        get => Settings.Processing.Gate; set => ApplyProcessing(Settings.Processing with { Gate = value }, nameof(Gate));
    }
    public double GateThreshold
    {
        get => Settings.Processing.GateThreshold; set => ApplyProcessing(Settings.Processing with { GateThreshold = value }, nameof(GateThreshold));
    }
    public double GateAttack
    {
        get => Settings.Processing.GateAttackMs; set => ApplyProcessing(Settings.Processing with { GateAttackMs = value }, nameof(GateAttack));
    }
    public double GateRelease
    {
        get => Settings.Processing.GateReleaseMs; set => ApplyProcessing(Settings.Processing with { GateReleaseMs = value }, nameof(GateRelease));
    }
    public int NoiseSuppression
    {
        get => Settings.Processing.NoiseSuppression; set => ApplyProcessing(Settings.Processing with { NoiseSuppression = value }, nameof(NoiseSuppression));
    }
    public bool Compressor
    {
        get => Settings.Processing.Compressor; set => ApplyProcessing(Settings.Processing with { Compressor = value }, nameof(Compressor));
    }
    public double CompressorThreshold
    {
        get => Settings.Processing.CompressorThreshold; set => ApplyProcessing(Settings.Processing with { CompressorThreshold = value }, nameof(CompressorThreshold));
    }
    public double CompressorRatio
    {
        get => Settings.Processing.CompressorRatio; set => ApplyProcessing(Settings.Processing with { CompressorRatio = value }, nameof(CompressorRatio));
    }
    public bool AutoGain
    {
        get => Settings.Processing.AutoGain; set => ApplyProcessing(Settings.Processing with { AutoGain = value }, nameof(AutoGain));
    }
    public double Bass
    {
        get => Settings.Processing.BassDb; set => ApplyProcessing(Settings.Processing with { BassDb = value }, nameof(Bass));
    }
    public double Mid
    {
        get => Settings.Processing.MidDb; set => ApplyProcessing(Settings.Processing with { MidDb = value }, nameof(Mid));
    }
    public double Treble
    {
        get => Settings.Processing.TrebleDb; set => ApplyProcessing(Settings.Processing with { TrebleDb = value }, nameof(Treble));
    }
    private void ApplyProcessing(ProcessingSettings value, string name)
    {
        Settings.Processing = value;
        graph.Processor.Settings = value;
        Changed(name);
    }
    [RelayCommand]
    private void ResetProcessing()
    {
        Settings.Processing = new();
        graph.Processor.Settings = Settings.Processing;
        GainDb = 0;
        foreach (var name in new[] { nameof(Gate), nameof(GateThreshold), nameof(GateAttack), nameof(GateHold), nameof(GateRelease), nameof(NoiseSuppression), nameof(Compressor), nameof(CompressorThreshold), nameof(CompressorRatio), nameof(CompressorAttack), nameof(CompressorRelease), nameof(MakeupGain), nameof(AutoGain), nameof(Bass), nameof(Mid), nameof(Treble) })
            OnPropertyChanged(name);
        Save();
    }
}
