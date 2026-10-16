using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using MyVoice.Core;
namespace MyVoice.App.Services;

/// <summary>Transient key state only, on a dedicated message thread. Never translates or stores typed text.</summary>
public sealed class GlobalKeyListener : IDisposable
{
    private delegate IntPtr Callback(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct Message {public IntPtr Window;public uint Id;public UIntPtr W;public IntPtr L;public uint Time;public int X,Y;public uint Private;}
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int kind,Callback callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message,IntPtr window,uint first,uint last);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message,IntPtr window,uint first,uint last,uint remove);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread,uint message,UIntPtr w,IntPtr l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    private readonly Thread thread;
    private readonly Action<KeyboardKeyKind,bool> play;
    private readonly bool[] held=new bool[256];
    private readonly ManualResetEventSlim ready=new();
    private readonly Callback callback;
    private IntPtr hook;private uint threadId;private Exception? failure;
    public KeyboardSettings Settings {get;set;}=new();
    public GlobalKeyListener(Action<KeyboardKeyKind,bool> play)
    {
        this.play=play;callback=Hook;thread=new Thread(Run){IsBackground=true,Name="MyVoice keyboard sound"};thread.Start();
        ready.Wait();if(failure!=null){ready.Dispose();throw failure;}
    }
    private void Run()
    {
        threadId=GetCurrentThreadId();PeekMessage(out _,IntPtr.Zero,0,0,0);
        hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
        if(hook==IntPtr.Zero)failure=new Win32Exception(Marshal.GetLastWin32Error());ready.Set();
        try{if(hook!=IntPtr.Zero)while(GetMessage(out _,IntPtr.Zero,0,0)>0){}}
        finally{if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);hook=IntPtr.Zero;Array.Clear(held);}
    }
    private IntPtr Hook(int code,IntPtr message,IntPtr data)
    {
        if(code>=0)
        {
            int key=Marshal.ReadInt32(data);int msg=message.ToInt32();bool down=msg is 0x100 or 0x104,up=msg is 0x101 or 0x105;
            if(key is >0 and <256&&(down||up))
            {
                bool wasHeld=held[key];held[key]=down;var options=Settings;
                bool modifier=key is 16 or 17 or 18 or 91 or 92 or >=160 and <=165;
                if(key is not(>=0xA6 and <=0xB7)&&(!modifier||!options.ExcludeModifiers)&&(!(key is >=112 and <=135)||!options.ExcludeFunctionKeys)&&(down?(!wasHeld||options.Repeat):wasHeld))
                {
                    var kind=key switch{32=>KeyboardKeyKind.Space,13=>KeyboardKeyKind.Enter,8=>KeyboardKeyKind.Backspace,16 or 160 or 161=>KeyboardKeyKind.Shift,_ when modifier=>KeyboardKeyKind.Modifier,27 or 9 or >=112 and <=135=>KeyboardKeyKind.Small,_=>KeyboardKeyKind.Normal};
                    try{play(kind,up);}catch{/* Never interfere with the user's keyboard. */}
                }
            }
        }
        return CallNextHookEx(hook,code,message,data);
    }
    public void Dispose(){PostThreadMessage(threadId,0x12,UIntPtr.Zero,IntPtr.Zero);thread.Join();ready.Dispose();}
}
