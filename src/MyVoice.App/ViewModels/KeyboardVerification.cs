using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    internal async Task VerifyKeyboardStateAsync()
    {
        if(enableOutputs)throw new InvalidOperationException("Isolated profile required");
        if(graph.Keyboard.LoadedBytes!=0)throw new Exception("Preview retained samples while OFF");
        KeyboardEnabled=true;await ApplyKeyboardAsync();
        if(graph.Keyboard.LoadedBytes<=0||!KeyboardMonitor||KeyboardVirtual)throw new Exception("Keyboard default routing or load failed");
        var window=(MainWindow)Application.Current.MainWindow;window.Hide();
        graph.Keyboard.Trigger(KeyboardKeyKind.Space,false);await Task.Delay(30);
        if(graph.Mixer.BusMeter("monitor").PeakDb<=-90||graph.Mixer.BusMeter("virtual").PeakDb>-90)throw new Exception("Keyboard tray routing failed");
        window.Reveal();KeyboardVolume=125;KeyboardPitch=2;Save();
        var loaded=new MyVoice.Infrastructure.MixerStore(store.Root).Load(Settings);
        if(loaded.Current.Keyboard.Pack!="blue"||loaded.Current.Keyboard.Volume!=1.25||loaded.Current.Keyboard.Pitch!=2)throw new Exception("Keyboard settings not saved");
        KeyboardEnabled=false;await ApplyKeyboardAsync();
        if(graph.Keyboard.LoadedBytes!=0||keyboardListener!=null)throw new Exception("Keyboard OFF retained resources");
    }
}
