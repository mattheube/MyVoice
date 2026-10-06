using System;
using Microsoft.Win32;
namespace MyVoice.App.Services;
public static class StartupService
{
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
            key.SetValue("MyVoice", $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue("MyVoice", false);
    }
}
