using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Interop;
using MyVoice.App.Services;
namespace MyVoice.App;
public partial class App
{
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    private async Task VerifyBackground(MainWindow window,string folder)
    {
        window.Hide();await Task.Delay(100);
        if(UiActivity.State!=UiActivityState.Tray)throw new Exception("Hidden window did not enter tray visual policy");
        var sound=vm!.SoundCards[0];var loop=sound.Item.Loop;sound.Item.Loop=true;
        using var hidden=new HwndSource(new HwndSourceParameters("MyVoice background hotkey verification"));
        using var keys=new HotkeyService(hidden.Handle);bool delivered=false;
        keys.ActionPressed+=action=>{if(action=="sound"){delivered=true;vm.PlaySoundCommand.Execute(sound);}};
        try
        {
            if(!keys.SetAction("sound",7,0x7A))throw new Exception("Test hotkey unavailable");
            if(!PostMessage(hidden.Handle,0x312,(IntPtr)100,IntPtr.Zero))throw new Exception("Hotkey message failed");
            await Task.Delay(300);
            if(!delivered||!vm.VerifySoundPlaying(sound))throw new Exception("Soundboard hotkey did not play while hidden");
            await Task.Delay(2500);
            if(!vm.VerifySoundPlaying(sound))throw new Exception("Background sound stopped");
            File.WriteAllText(Path.Combine(folder,"background-result.txt"),"PASS registered hidden HWND receives WM_HOTKEY and starts a looping sound through the app command; playback continues in tray; visual policy leaves audio running. Synthetic message, not physical keyboard input.");
        }
        finally{vm.StopAllSounds();sound.Item.Loop=loop;window.Reveal();}
    }
}
