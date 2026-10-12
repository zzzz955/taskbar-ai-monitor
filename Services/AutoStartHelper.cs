using System;
using Microsoft.Win32;

namespace TaskbarAiMonitor.Services;

public static class AutoStartHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "TaskbarAiMonitor";

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exePath) && exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var appExe = System.IO.Path.ChangeExtension(dllPath, ".exe");
                    if (System.IO.File.Exists(appExe))
                    {
                        exePath = appExe;
                    }
                }

                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch { }
    }
}
