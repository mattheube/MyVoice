using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MyVoice.App.Services;
namespace MyVoice.App.ViewModels;
public sealed record ShortcutRow(string Action,string Name,string Shortcut);
public partial class MainViewModel
{
    public IEnumerable<ShortcutRow> ShortcutRows
    {
        get
        {
            var actions=new Dictionary<string,string>
            {
                ["mute"]=T["Mute"],["monitor"]=T["HearMyself"],["stop"]=T["StopAll"],
                ["effects"]=T["VoiceEnabled"],["next"]=T["ShortcutNextVoice"],["previous"]=T["ShortcutPreviousVoice"],
                ["open"]="MyVoice",["keyboard:toggle"]=T["KeyboardTitle"]
            };
            foreach(var sound in SoundCards)actions["sound:"+sound.Item.Id]=T["ShortcutPlay"]+" · "+sound.Name;
            foreach(var action in Settings.Shortcuts.Keys)
                if(!actions.ContainsKey(action))actions[action]=action;
            return actions.Select(a=>new ShortcutRow(a.Key,a.Value,ShortcutLabel(a.Key))).ToArray();
        }
    }
    private string ShortcutLabel(string action)=>action=="mute"
        ?Settings.HotkeyKey==0?T["Unassigned"]:HotkeyService.Label(Settings.HotkeyModifiers,Settings.HotkeyKey)
        :Settings.Shortcuts.TryGetValue(action,out var key)?HotkeyService.Label(key.Modifiers,key.Key):T["Unassigned"];
    public string SelectedSoundShortcutAction=>SelectedSound==null?"none":"sound:"+SelectedSound.Item.Id;
    public string SelectedSoundShortcutLabel=>ShortcutLabel(SelectedSoundShortcutAction);
    public void PauseShortcutActions()=>hotkey?.Pause();
    public void ResumeShortcutActions(){if(hotkey?.Resume()==false)ShowToast(T["Conflict"]);}
    [RelayCommand] private void ClearShortcut(string action)=>SetShortcut(action,0,0);
}
