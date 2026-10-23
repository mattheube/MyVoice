using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace MyVoice.App.Services;

// Windows' compositor supplies the real blur; no screenshots, bitmap capture or render loop.
public sealed class NativeBackdrop : IDisposable
{
    public static bool Supported=>OperatingSystem.IsWindowsVersionAtLeast(10,0,22621);
    private readonly Window window;
    private readonly IntPtr handle;
    public bool Active{get;private set;}
    public NativeBackdrop(Window window)
    {
        this.window=window;handle=new WindowInteropHelper(window).Handle;
        ProductAppearance.Changed+=Changed;Apply();
    }
    private void Changed(object? sender,EventArgs args)=>Apply();
    private void Apply()
    {
        if(!Supported||handle==IntPtr.Zero)return;
        bool enable=ProductAppearance.GlassActive;
        var chrome=WindowChrome.GetWindowChrome(window);
        if(chrome!=null)chrome.GlassFrameThickness=enable?new Thickness(-1):new Thickness(0);
        var margins=new Margins{Left=enable?-1:0,Right=enable?-1:0,Top=enable?-1:0,Bottom=enable?-1:0};
        int type=enable?3:1; // DWMSBT_TRANSIENTWINDOW (Acrylic) / DWMSBT_NONE.
        int frame=DwmExtendFrameIntoClientArea(handle,ref margins);
        int result=DwmSetWindowAttribute(handle,38,ref type,sizeof(int));
        Active=enable&&frame>=0&&result>=0;
        var target=HwndSource.FromHwnd(handle)?.CompositionTarget;
        if(target!=null)target.BackgroundColor=Active?Colors.Transparent:Colors.Black;
        if(enable&&!Active)ProductAppearance.DisableNativeGlass();
    }
    public int ReadNativeType(){if(!Supported)return 1;return DwmGetWindowAttribute(handle,38,out int value,sizeof(int))>=0?value:1;}
    public void Dispose(){ProductAppearance.Changed-=Changed;}
    [StructLayout(LayoutKind.Sequential)]private struct Margins{public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")]private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
    [DllImport("dwmapi.dll")]private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
}
