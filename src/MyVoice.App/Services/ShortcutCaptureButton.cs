using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MyVoice.App.ViewModels;
namespace MyVoice.App.Services;
public sealed class ShortcutCaptureButton:Button
{
    public static readonly DependencyProperty ActionIdProperty=DependencyProperty.Register(nameof(ActionId),typeof(string),typeof(ShortcutCaptureButton),new PropertyMetadata("none"));
    public string ActionId {get=>(string)GetValue(ActionIdProperty);set=>SetValue(ActionIdProperty,value);}
    private readonly HashSet<Key> keys=[];
    private readonly HashSet<Key> held=[];
    private readonly DispatcherTimer deadline=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly DispatcherTimer idle=new(){Interval=TimeSpan.FromMilliseconds(140)};
    private bool capturing;
    public bool IsCapturing=>capturing;
    internal Func<ModifierKeys> ReadModifiers {get;set;}=()=>Keyboard.Modifiers;
    private MainViewModel? vm;
    private string action="none";
    public ShortcutCaptureButton()
    {
        SetResourceReference(StyleProperty,typeof(Button));
        Click+=(_,_)=>Begin();deadline.Tick+=(_,_)=>Finish();idle.Tick+=(_,_)=>Finish();
        Unloaded+=(_,_)=>Cancel();LostKeyboardFocus+=(_,_)=>Cancel();
    }
    private void Begin()
    {
        if(Window.GetWindow(this)?.DataContext is not MainViewModel model)return;
        Cancel();Focus();vm=model;action=ActionId;capturing=true;keys.Clear();held.Clear();
        vm.PauseShortcutActions();SetCurrentValue(ContentProperty,vm.T["Press"]);
    }
    private static Key Normalize(Key key)=>key switch
    {Key.RightCtrl=>Key.LeftCtrl,Key.RightShift=>Key.LeftShift,Key.RightAlt=>Key.LeftAlt,Key.RWin=>Key.LWin,_=>key};
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if(!capturing){base.OnPreviewKeyDown(e);return;}
        e.Handled=true;var key=Normalize(e.Key==Key.System?e.SystemKey:e.Key);
        if(key==Key.Escape){Cancel();return;}
        if(keys.Count==0)deadline.Start();
        idle.Stop();keys.Add(key);held.Add(key);
        foreach(var (modifier,code) in new[]{(ModifierKeys.Control,Key.LeftCtrl),(ModifierKeys.Alt,Key.LeftAlt),(ModifierKeys.Shift,Key.LeftShift),(ModifierKeys.Windows,Key.LWin)})
            if(ReadModifiers().HasFlag(modifier)){keys.Add(code);held.Add(code);}
        if(keys.Count>3){var model=vm;Cancel();model?.ShowToast(model.T["ShortcutLimit"]);return;}
    }
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if(!capturing){base.OnPreviewKeyUp(e);return;}
        e.Handled=true;held.Remove(Normalize(e.Key==Key.System?e.SystemKey:e.Key));
        if(held.Count==0&&keys.Count>0)idle.Start();
    }
    private void Finish()
    {
        if(!capturing)return;
        var model=vm;var captured=keys.ToArray();var target=action;Cancel();
        var normal=captured.Where(k=>k is not(Key.LeftCtrl or Key.LeftAlt or Key.LeftShift or Key.LWin)).ToArray();
        if(normal.Length!=1){model?.ShowToast(model.T["ShortcutSupported"]);return;}
        uint mods=(uint)((captured.Contains(Key.LeftAlt)?1:0)|(captured.Contains(Key.LeftCtrl)?2:0)|(captured.Contains(Key.LeftShift)?4:0)|(captured.Contains(Key.LWin)?8:0));
        if(model?.SetShortcut(target,mods,(uint)KeyInterop.VirtualKeyFromKey(normal[0]))==false)model.ShowToast(model.T["Conflict"]);
    }
    private void Cancel()
    {
        deadline.Stop();idle.Stop();
        if(capturing){capturing=false;vm?.ResumeShortcutActions();}
        GetBindingExpression(ContentProperty)?.UpdateTarget();vm=null;
    }
}
