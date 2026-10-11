using System.Runtime.InteropServices;
namespace MyVoice.App.Services;
public static class WindowsIdentity
{
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    public static void Initialize()=>Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID("MyVoice.App"));
}
