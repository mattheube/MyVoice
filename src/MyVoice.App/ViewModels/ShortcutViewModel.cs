using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MyVoice.Core;
using MyVoice.App.Services;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    public string[] HotkeyTargets { get; } = ["Mute microphone", "Hear myself", "Stop all sounds", "Voice on/off", "Next voice", "Previous voice", "Open MyVoice", "Selected sound", "Selected voice"];
    [ObservableProperty] private string hotkeyTarget = "Mute microphone";
    partial void OnHotkeyTargetChanged(string value) => OnPropertyChanged(nameof(HotkeyLabel));
    private string ShortcutAction => HotkeyTarget switch { "Hear myself" => "monitor", "Stop all sounds" => "stop", "Voice on/off" => "effects", "Next voice" => "next", "Previous voice" => "previous", "Open MyVoice" => "open", "Selected sound" => SelectedSound == null ? "none" : "sound:" + SelectedSound.Item.Id, "Selected voice" => "voice:" + CurrentPreset, _ => "mute" };
    private string ReadShortcutLabel()
    {
        if (ShortcutAction == "mute")
            return Settings.HotkeyKey == 0 ? T["Unassigned"] : HotkeyService.Label(Settings.HotkeyModifiers, Settings.HotkeyKey);
        return Settings.Shortcuts.TryGetValue(ShortcutAction, out var b) ? HotkeyService.Label(b.Modifiers, b.Key) : T["Unassigned"];
    }
    private void AttachStudioHotkeys(HotkeyService service)
    {
        service.ActionPressed += OnStudioHotkey;
        foreach (var binding in Settings.Shortcuts)
        if (!service.SetAction(binding.Key, binding.Value.Modifiers, binding.Value.Key))
            Status = T["Conflict"];
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private readonly System.Collections.Generic.Dictionary<Guid, uint> heldSounds = new();
    private void ReleaseHeldSounds()
    {
        foreach (var pair in heldSounds.ToArray())
        if ((GetAsyncKeyState((int)pair.Value) & 0x8000) == 0)
        {
            sounds.Stop(pair.Key);
            heldSounds.Remove(pair.Key);
        }
    }
    public void ReleaseSound(SoundCard card) => sounds.Stop(card.Item.Id);
    private void OnStudioHotkey(string action)
    {
        if (action.StartsWith("sound:") && Guid.TryParse(action[6..], out var id))
        {
            var card = SoundCards.FirstOrDefault(s => s.Item.Id == id);
            PlaySound(card);
            if (card?.Item.PlaybackMode == "Hold" && Settings.Shortcuts.TryGetValue(action, out var binding))
                heldSounds[id] = binding.Key;
            return;
        }
        if (action.StartsWith("voice:"))
        {
            SelectPreset(Presets.FirstOrDefault(p => p.Name == action[6..]));
            return;
        }
        switch (action)
        {
            case "monitor":
                HearMyself = !HearMyself;
                break;
            case "stop":
                StopAllSounds();
                break;
            case "effects":
                VoiceEnabled = !VoiceEnabled;
                break;
            case "next":
                SelectPreset(Presets[(Presets.ToList().FindIndex(p => p.Name == CurrentPreset) + 1) % Presets.Count]);
                break;
            case "previous":
                SelectPreset(Presets[(Presets.ToList().FindIndex(p => p.Name == CurrentPreset) + Presets.Count - 1) % Presets.Count]);
                break;
            case "open":
                ((MainWindow)System.Windows.Application.Current.MainWindow).Reveal();
                break;
        }
    }
}
