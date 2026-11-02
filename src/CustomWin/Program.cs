using CustomWin.Core;
using CustomWin.UI;

namespace CustomWin;

internal static class Program
{
    private const string MutexName = @"Local\CustomWin.SingleInstance";
    internal const string ShowEventName = @"Local\CustomWin.Show";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            // Уже запущено — просим открыть окно настроек
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            return;
        }

        ApplicationConfiguration.Initialize();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        var context = new TrayContext(args);
        Application.ThreadException += (_, e) => context.ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, _) => context.EmergencyRestore();

        Application.Run(context);
    }
}
