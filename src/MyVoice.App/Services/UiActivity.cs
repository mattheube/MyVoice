using System;
namespace MyVoice.App.Services;
public enum UiActivityState { Foreground, Background, Tray }
// Visual activity only. Audio services and inference never subscribe to this policy.
public static class UiActivity
{
    public static UiActivityState State { get; private set; } = UiActivityState.Foreground;
    public static bool VisualsActive => State == UiActivityState.Foreground;
    public static event EventHandler? Changed;
    public static void Set(UiActivityState state)
    {
        if(State==state)return;State=state;Changed?.Invoke(null,EventArgs.Empty);
    }
}
