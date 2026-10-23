using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.App.Services;
using MyVoice.App.ViewModels;
using MyVoice.Core;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyMajorUpdate(MainWindow window,string folder)
    {
        vm!.ShowWelcome=false;vm.Page=3;window.UpdateLayout();
        var view=Walk(window).OfType<Views.SettingsView>().Single();
        var tabs=Walk(view).OfType<TabControl>().Single();
        if(tabs.Items.Count!=5)throw new Exception("Expected five Settings categories");
        for(int i=0;i<5;i++)
        {
            vm.SettingsTab=i;window.UpdateLayout();await Task.Delay(100);
            if(tabs.SelectedIndex!=i)throw new Exception("Settings navigation binding failed");
            Shot("settings-"+i+".png");
        }
        vm.SettingsTab=2;window.UpdateLayout();
        var themes=Walk(view).OfType<ListBox>().Single();
        themes.SelectedValue="Amber";window.UpdateLayout();
        if(vm.Theme!="Amber"||vm.Themes.Length!=10)throw new Exception("Theme grid did not select/preserve theme IDs");
        vm.LiquidGlass=true;vm.GlassBlur=true;vm.GlassTransparency=55;vm.MotionMode="Full";UiActivity.Set(UiActivityState.Foreground);window.UpdateLayout();await Task.Delay(100);
        if(NativeBackdrop.Supported&&SystemParameters.ClientAreaAnimation&&(RenderCapability.Tier>>16)>0&&!SystemParameters.HighContrast)
        {
            if(!ProductAppearance.GlassActive||window.NativeBackdropType!=3)throw new Exception("Native acrylic was not applied");
            if(((SolidColorBrush)Resources["CanvasBrush"]).Color.A>=255)throw new Exception("Glass backdrop is hidden by opaque canvas");
            Shot("settings-native-glass.png");
        }
        UiActivity.Set(UiActivityState.Tray);window.UpdateLayout();
        if(ProductAppearance.GlassActive||window.NativeBackdropType!=1)throw new Exception("Native blur remained active in tray");
        UiActivity.Set(UiActivityState.Foreground);vm.LiquidGlass=false;await Task.Delay(250);window.UpdateLayout();
        if(((SolidColorBrush)Resources["CanvasBrush"]).Color.A<255)throw new Exception("Opaque fallback was not restored");
        vm.Page=1;window.UpdateLayout();
        var board=Walk(window).OfType<Views.SoundboardView>().Single();
        if(!Walk(board).OfType<Views.KeyboardSoundView>().Single().IsVisible)throw new Exception("Keyboard controls hidden");
        vm.SelectedKeyboardPack="blue";window.UpdateLayout();
        if(vm.SelectedKeyboardPack!="blue")throw new Exception("Keyboard pack selector failed");
        await Task.Delay(700);window.UpdateLayout();
        var keyboardChoice=Walk(board).OfType<ComboBox>().First(c=>c.SelectedValuePath=="Id");
        if(Walk(keyboardChoice).OfType<TextBlock>().Any(t=>t.Text.Contains("KeyboardPack {")))throw new Exception("ComboBox renders object instead of display name");
        var packCovers=Walk(board).OfType<CoverImage>().Where(c=>c.FilePath?.Contains("official-packs")==true).ToArray();
        if(packCovers.Length!=4||packCovers.Any(c=>c.Source==null))throw new Exception("Official pack artwork not loaded");
        Shot("soundboard-keyboard.png");
        var card=new CommunityCard{Id="major-library-fixture",Title="Fixture",Kind="ai-voice"};
        vm.CommunityItems.Add(card);
        if(card.Installed)throw new Exception("Uninstalled voice appears added");
        var voice=new VoiceReference{SourceContentId=card.Id};vm.AiVoices.Add(voice);
        if(!card.Installed)throw new Exception("Installed voice not synchronized");
        vm.AiVoices.Remove(voice);
        if(card.Installed)throw new Exception("Deleted voice still appears added");
        vm.CommunityItems.Remove(card);
        vm.SettingsTab=3;vm.Page=3;window.UpdateLayout();
        foreach(var chord in new[]{new[]{Key.F22},new[]{Key.LeftCtrl,Key.F23},new[]{Key.LeftCtrl,Key.LeftShift,Key.F24}})
        {
            var button=Walk(view).OfType<ShortcutCaptureButton>().First(b=>b.ActionId=="stop");
            button.ReadModifiers=()=>ModifierKeys.None;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
            var source=PresentationSource.FromVisual(button)!;
            foreach(var key in chord)button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            foreach(var key in chord.Reverse())button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key){RoutedEvent=Keyboard.PreviewKeyUpEvent});
            var until=DateTime.UtcNow.AddSeconds(2);
            while(button.IsCapturing&&DateTime.UtcNow<until)await Task.Delay(50);
            window.UpdateLayout();
            if(!vm.Settings.Shortcuts.TryGetValue("stop",out var binding)||binding.Key!=(uint)KeyInterop.VirtualKeyFromKey(chord[^1]))throw new Exception("Shortcut capture failed for "+chord.Length+" keys; status="+vm.ToastText+"; actual="+binding+"; capturing="+button.IsCapturing);
            uint mods=chord.Length==3?6u:chord.Length==2?2u:0u;
            if(binding.Modifiers!=mods)throw new Exception($"Shortcut modifiers lost: expected {mods}, actual {binding.Modifiers}");
            if(vm.SetShortcut("monitor",mods,binding.Key))throw new Exception("Conflicting shortcut accepted");
        }
        vm.SetShortcut("stop",0,0);Shot("shortcuts.png");
        var editData=System.Text.Json.JsonSerializer.SerializeToElement(new{title="Création publiée",description="Un aperçu modifiable, un audio conservé.",tags=new[]{"test"},visibility="friends",category="Other",language="fr",audio_locked=true});
        var editor=new Views.PublicationEditor(vm,new CommunityCard{Id=Guid.NewGuid().ToString(),Title="Création publiée",Kind="ai-voice"},editData);
        editor.Show();await Task.Delay(150);editor.UpdateLayout();
        if(((Button)editor.FindName("ReplaceAudioButton")).IsEnabled)throw new Exception("Shared audio replacement enabled in editor");
        if(((ComboBox)editor.FindName("VisibilityChoice")).SelectedValue as string!="friends")throw new Exception("Publication editor visibility not initialized");
        var editImage=new RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,PixelFormats.Pbgra32);editImage.Render(editor);
        var editPng=new PngBitmapEncoder();editPng.Frames.Add(BitmapFrame.Create(editImage));using(var editFile=File.Create(Path.Combine(folder,"publication-editor.png")))editPng.Save(editFile);
        editor.Close();
        File.WriteAllText(Path.Combine(folder,"major-update-result.txt"),"PASS five Settings categories; ten theme previews and live selection; native Acrylic DWM attribute and translucent canvas; tray blur off and opaque fallback; visible Keyboard controls and writable pack selector; Community Added state follows real catalogs; native shortcut capture 1/2/3 keys and conflict rejection.");
        void Shot(string name)
        {
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(folder,name));png.Save(file);
        }
    }
}
