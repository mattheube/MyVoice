using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
namespace MyVoice.App.Services;
public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private readonly HwndSource source;
    private readonly Dictionary<string, (int Id, uint Mods, uint Key)> bindings = new();
    private int nextId = 100;
    public event Action? Pressed;
    public event Action<string>? ActionPressed;
    public HotkeyService(IntPtr handle)
    {
        source = HwndSource.FromHwnd(handle)!;
        source.AddHook(Hook);
    }
    public bool Set(uint modifiers, uint key) => SetAction("mute", modifiers, key);
    public bool SetAction(string action, uint modifiers, uint key)
    {
        if (key == 0)
        {
            if (bindings.Remove(action, out var old))
                UnregisterHotKey(source.Handle, old.Id);
            return true;
        }
        if (bindings.TryGetValue(action, out var current) && current.Mods == modifiers && current.Key == key)
            return true;
        if (bindings.Any(b => b.Key != action && b.Value.Mods == modifiers && b.Value.Key == key))
            return false;
        int id = nextId++;
        if (!RegisterHotKey(source.Handle, id, modifiers | 0x4000, key))
            return false;
        if (bindings.Remove(action, out var prior))
            UnregisterHotKey(source.Handle, prior.Id);
        bindings[action] = (id, modifiers, key);
        return true;
    }
    public void Clear() => Set(0, 0);
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x312)
        {
            var binding = bindings.FirstOrDefault(b => b.Value.Id == wParam.ToInt32());
            if (binding.Key != null)
            {
                if (binding.Key == "mute")
                    Pressed?.Invoke();
                else
                    ActionPressed?.Invoke(binding.Key);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }
    public static string Label(uint mods, uint key) => $"{((mods & 2) != 0 ? "Ctrl + " : "")}{((mods & 1) != 0 ? "Alt + " : "")}{((mods & 4) != 0 ? "Shift + " : "")}{KeyInterop.KeyFromVirtualKey((int)key)}";
    public void Dispose()
    {
        foreach (var b in bindings.Values)
            UnregisterHotKey(source.Handle, b.Id);
        bindings.Clear();
        source.RemoveHook(Hook);
    }
}
