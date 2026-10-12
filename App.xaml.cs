using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace TaskbarAiMonitor;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogException("AppDomain", args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogException("Dispatcher", args.Exception);
            args.Handled = true;
        };

        base.OnStartup(e);
    }

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] [{source}] {ex?.ToString()}\n");
        }
        catch { }
    }
}
