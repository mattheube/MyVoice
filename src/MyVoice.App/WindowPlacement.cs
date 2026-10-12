using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
namespace MyVoice.App;
public partial class MainWindow
{
    private bool fullscreen;
    private Rect beforeFullscreen;
    private WindowState beforeFullscreenState;
    private ResizeMode beforeFullscreenResize;
    private WindowChrome? savedChrome;
    private HwndSource? windowSource;
    private void InitializePlacement()
    {
        SourceInitialized+=(_,_)=>{windowSource=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);windowSource?.AddHook(WindowMessage);};
        Closed+=(_,_)=>windowSource?.RemoveHook(WindowMessage);
        StateChanged+=(_,_)=>UpdateCaption();
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.F11){ToggleFullscreen();e.Handled=true;}else if(e.Key==Key.Escape&&fullscreen){ToggleFullscreen();e.Handled=true;}};
        UpdateCaption();
    }
    private void UpdateCaption()
    {
        MaximizeGlyph.Data=System.Windows.Media.Geometry.Parse(WindowState==WindowState.Maximized || fullscreen ? "M 3,4 V 1 H 12 V 10 H 9 M 1,4 H 9 V 12 H 1 Z" : "M 1,1 H 12 V 12 H 1 Z");
        MaximizeButton.ToolTip=WindowState==WindowState.Maximized || fullscreen ? "Restaurer" : "Agrandir";
    }
    internal void ToggleFullscreen()
    {
        if(!fullscreen)
        {
            beforeFullscreenState=WindowState;beforeFullscreen=WindowState==WindowState.Normal?new Rect(Left,Top,Width,Height):RestoreBounds;
            beforeFullscreenResize=ResizeMode;savedChrome=WindowChrome.GetWindowChrome(this);
            var info=MonitorInfo();WindowState=WindowState.Normal;fullscreen=true;
            WindowChrome.SetWindowChrome(this,null);ResizeMode=ResizeMode.NoResize;
            var source=PresentationSource.FromVisual(this);var scale=source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
            var location=scale.Transform(new Point(info.Monitor.Left,info.Monitor.Top));var size=scale.Transform(new Vector(info.Monitor.Right-info.Monitor.Left,info.Monitor.Bottom-info.Monitor.Top));
            Left=location.X;Top=location.Y;Width=size.X;Height=size.Y;
        }
        else
        {
            fullscreen=false;WindowChrome.SetWindowChrome(this,savedChrome);ResizeMode=beforeFullscreenResize;
            WindowState=WindowState.Normal;Left=beforeFullscreen.Left;Top=beforeFullscreen.Top;Width=beforeFullscreen.Width;Height=beforeFullscreen.Height;WindowState=beforeFullscreenState;
        }
        UpdateCaption();
    }
    private nint WindowMessage(nint hwnd,int msg,nint wParam,nint lParam,ref bool handled)
    {
        if(msg==0x020E)handled=Services.HorizontalScroll.HorizontalWheel((short)((long)wParam>>16));
        if(msg==0x0024 && !fullscreen)
        {
            var info=MonitorInfo();var data=Marshal.PtrToStructure<MinMax>(lParam);
            data.MaxPosition=new(info.Work.Left-info.Monitor.Left,info.Work.Top-info.Monitor.Top);
            data.MaxSize=new(info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
            Marshal.StructureToPtr(data,lParam,false);handled=true;
        }
        return 0;
    }
    internal void VerifyNativeBounds(bool full)
    {
        var info=MonitorInfo();var expected=full?info.Monitor:info.Work;
        if(!GetWindowRect(new WindowInteropHelper(this).Handle,out var actual))throw new System.ComponentModel.Win32Exception();
        if(Math.Abs(actual.Left-expected.Left)>2||Math.Abs(actual.Top-expected.Top)>2||Math.Abs(actual.Right-expected.Right)>2||Math.Abs(actual.Bottom-expected.Bottom)>2)
            throw new Exception($"Window bounds mismatch: {actual.Left},{actual.Top},{actual.Right},{actual.Bottom}; expected {expected.Left},{expected.Top},{expected.Right},{expected.Bottom}");
    }
    private MonitorData MonitorInfo(){var value=new MonitorData{Size=Marshal.SizeOf<MonitorData>()};if(!GetMonitorInfo(MonitorFromWindow(new WindowInteropHelper(this).Handle,2),ref value))throw new System.ComponentModel.Win32Exception();return value;}
    [StructLayout(LayoutKind.Sequential)]private struct NativePoint{public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MinMax{public NativePoint Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorData{public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool GetWindowRect(nint window,out NativeRect rect);
    [DllImport("user32.dll",CharSet=CharSet.Auto,SetLastError=true)]private static extern bool GetMonitorInfo(nint monitor,ref MonitorData data);
}
