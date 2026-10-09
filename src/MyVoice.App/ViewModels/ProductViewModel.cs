using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Core;
using MyVoice.Infrastructure;
using MyVoice.App.Services;
namespace MyVoice.App.ViewModels;

public partial class MainViewModel
{
    private ProductStore productStore=null!;
    public ProductState Product {get;private set;}=new();
    public string[] Themes {get;}=["Midnight","Graphite","Deep Ocean","Warm Dark"];
    public string[] MotionModes {get;}=["Full","Reduced","Off"];
    public string[] BackgroundModes {get;}=["Minimal","Balanced","Rich"];
    public string Theme {get=>Product.Theme;set{Product.Theme=value;ApplyAppearance();}}
    public string Accent {get=>Product.Accent;set{if(!System.Text.RegularExpressions.Regex.IsMatch(value??"","^#[0-9a-fA-F]{6}$"))return;Product.Accent=value!;ApplyAppearance();}}
    public string MotionMode {get=>Product.Motion;set{Product.Motion=value;ApplyAppearance();}}
    public string BackgroundMode {get=>Product.Background;set{Product.Background=value;ApplyAppearance();}}
    public string ProfileName {get=>Product.DisplayName;set{Product.DisplayName=value;SaveProduct();OnPropertyChanged(nameof(Welcome));}}
    public string ProfileBio {get=>Product.Bio;set{Product.Bio=value;SaveProduct();}}
    public string? ProfileAvatar=>Product.Avatar;
    public string Welcome=>string.IsNullOrWhiteSpace(Product.DisplayName)?"Bienvenue chez vous.":"Bonjour, "+Product.DisplayName+".";
    public string LibrarySummary=>$"{SoundCards.Count} sons · {AiVoices.Count} voix IA · tout reste sur votre PC";
    public string PageTitle=>Page switch{0=>"Voice",1=>"Soundboard",2=>"Studio",3=>"Settings",4=>"Home",5=>"Community",6=>"Mic Setup",7=>"Profile",_=>"MyVoice"};
    [ObservableProperty] private bool showWelcome;
    [ObservableProperty] private bool advancedMicrophone;
    [ObservableProperty] private bool favoriteSoundsOnly;
    partial void OnFavoriteSoundsOnlyChanged(bool value)=>SoundView?.Refresh();

    [ObservableProperty] private int studioTab=1;
    [ObservableProperty] private string toastText="";
    public ObservableCollection<DesignedVoice> DesignedVoices {get;}=[];
    [ObservableProperty] private string designerName="Ma nouvelle voix";
    [ObservableProperty] private string designerBase="Clean";
    [ObservableProperty] private double designerPitch=1;
    [ObservableProperty] private double designerBass;
    [ObservableProperty] private double designerMid;
    [ObservableProperty] private double designerTreble;
    [ObservableProperty] private double designerDistortion;
    [ObservableProperty] private double designerDelay;
    [ObservableProperty] private double designerMix=100;
    public string[] DesignerBases=>Presets.Select(p=>p.Name).ToArray();
    private void InitializeProduct()
    {
        productStore=new ProductStore(store.Root);
        Product=productStore.Load();
        if(!Settings.Animations&&Product.Motion=="Full")Product.Motion="Off";
        foreach(var preset in Product.Presets)DesignedVoices.Add(preset);
        graph.Processor.Designed=Product.Presets.FirstOrDefault(p=>p.Id==Settings.ActiveDesignedVoiceId);
        foreach(var preset in Presets)preset.Selected=!Settings.ActiveAiVoice&&graph.Processor.Designed==null&&preset.Name==CurrentPreset;
        ProductAppearance.Apply(Product);
        ShowWelcome=!Product.OnboardingCompleted;
        InitializeCommunity();Page=4;
    }
    private void SaveProduct(){try{productStore?.Save(Product);}catch(Exception e){Error(e);}}
    private void ApplyAppearance(){ProductAppearance.Apply(Product);Settings.Animations=ProductAppearance.Animate;SaveProduct();OnPropertyChanged(nameof(Theme));OnPropertyChanged(nameof(Accent));OnPropertyChanged(nameof(MotionMode));OnPropertyChanged(nameof(BackgroundMode));}
    partial void OnPageChanged(int value){OnPropertyChanged(nameof(PageTitle));OnPropertyChanged(nameof(LibrarySummary));}
    [RelayCommand] private void ContinueLocally(){Product.OnboardingCompleted=true;ShowWelcome=false;SaveProduct();ShowToast("Votre espace local est prêt.");}
    [RelayCommand] private void OpenAccount(){ShowWelcome=false;Page=7;}
    [RelayCommand] private void CreateBoardInStudio(){Page=2;StudioTab=0;}
    [RelayCommand] private void GoVoiceLab(){Page=2;StudioTab=1;}
    [RelayCommand] private void OpenBoard(SoundFolder? folder){SelectedFolder=folder;Page=1;}
    [RelayCommand] private void QuitApplication()=>((App)Application.Current).ExitApplication();
    [RelayCommand] private void ChangeAvatar(){try{var path=ImportCover(true,Product.Avatar);if(path==null)return;Product.Avatar=path;SaveProduct();OnPropertyChanged(nameof(ProfileAvatar));}catch(Exception e){Error(e);}}
    [RelayCommand] private void CopyCableName(){Clipboard.SetText("CABLE Output (VB-Audio Virtual Cable)");ShowToast("Nom du microphone copié.");}
    public double GateHold {get=>Settings.Processing.GateHoldMs;set=>ApplyProcessing(Settings.Processing with{GateHoldMs=Math.Clamp(value,0,500)},nameof(GateHold));}
    public double CompressorAttack {get=>Settings.Processing.CompressorAttackMs;set=>ApplyProcessing(Settings.Processing with{CompressorAttackMs=Math.Clamp(value,1,100)},nameof(CompressorAttack));}
    public double CompressorRelease {get=>Settings.Processing.CompressorReleaseMs;set=>ApplyProcessing(Settings.Processing with{CompressorReleaseMs=Math.Clamp(value,20,1000)},nameof(CompressorRelease));}
    public double MakeupGain {get=>Settings.Processing.MakeupDb;set=>ApplyProcessing(Settings.Processing with{MakeupDb=Math.Clamp(value,-12,18)},nameof(MakeupGain));}
    [RelayCommand] private void ResetMicControl(string name){switch(name){case "Bass":Bass=0;break;case "Mid":Mid=0;break;case "Treble":Treble=0;break;case "Gate":GateThreshold=-48;GateAttack=5;GateHold=100;GateRelease=120;break;case "Compressor":CompressorThreshold=-18;CompressorRatio=3;CompressorAttack=8;CompressorRelease=150;MakeupGain=0;break;}}
    public string CableStatus=>VirtualDevice?.Name.Contains("CABLE",StringComparison.OrdinalIgnoreCase)==true && Devices.Any(d=>d.Name.Contains("CABLE Output",StringComparison.OrdinalIgnoreCase))?"Connecté · VB-Audio Virtual Cable":"Câble non détecté · vérifiez les deux périphériques";
    [RelayCommand] private void RefreshCable(){Refresh();RefreshOutputs();OnPropertyChanged(nameof(CableStatus));}
    [RelayCommand] private void PlayLastSound()=>PlaySound(SoundCards.FirstOrDefault(s=>s.Item.Id==Settings.LastSoundId));
    [RelayCommand] private void ToggleSoundFavorite(SoundCard? card){if(card==null)return;if(!Product.FavoriteSounds.Remove(card.Item.Id))Product.FavoriteSounds.Add(card.Item.Id);SaveProduct();ShowToast(Product.FavoriteSounds.Contains(card.Item.Id)?"Son ajouté aux favoris":"Favori retiré");SoundView.Refresh();}
    public void ShowToast(string text){ToastText=text;var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};timer.Tick+=(_,_)=>{if(ToastText==text)ToastText="";timer.Stop();};timer.Start();}
    [RelayCommand] private void SaveDesignedVoice()
    {
        if(string.IsNullOrWhiteSpace(DesignerName)){ShowToast("Donnez un nom à votre voix.");return;}
        var voice=new DesignedVoice{Name=DesignerName.Trim(),Base=DesignerBase,Pitch=DesignerPitch,Bass=DesignerBass,Mid=DesignerMid,Treble=DesignerTreble,Distortion=DesignerDistortion/100,Delay=DesignerDelay/100,Mix=DesignerMix/100};
        Product.Presets.Add(voice);DesignedVoices.Add(voice);SaveProduct();ShowToast("Voice Preset enregistré dans Voice.");
    }
    [RelayCommand] private void UseDesignedVoice(DesignedVoice? voice)
    {
        if(voice==null)return;
        SelectPreset(Presets.FirstOrDefault(p=>p.Name==voice.Base)??Presets[0]);
        foreach(var preset in Presets)preset.Selected=false;
        Settings.LastVoiceIsAi=false;graph.Processor.Designed=voice;Settings.LastDesignedVoiceId=voice.Id;Settings.ActiveDesignedVoiceId=voice.Id;
        OnPropertyChanged(nameof(DisplayVoice));OnPropertyChanged(nameof(VoiceSwitchLabel));Save();ShowToast(voice.Name+" activée");
    }
}
