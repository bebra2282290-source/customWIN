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

        // Порядок важен: режим DPI и обработку исключений можно задать только
        // до создания первого окна. WindowsFormsSynchronizationContext уже создаёт
        // скрытое окно, поэтому он идёт последним.
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        TrayContext context;
        try
        {
            context = new TrayContext(args);
        }
        catch (Exception ex)
        {
            ShowFatal(ex);
            return;
        }

        Application.ThreadException += (_, e) => context.ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            context.EmergencyRestore();
            if (e.ExceptionObject is Exception ex) ShowFatal(ex);
        };

        Application.Run(context);
    }

    /// <summary>Показать ошибку запуска вместо того, чтобы молча закрыться.</summary>
    private static void ShowFatal(Exception ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(Storage.DataDir, "error.log"), $"[{DateTime.Now:u}] FATAL {ex}\n\n");
        }
        catch { }
        MessageBox.Show("CustomWin не смог запуститься:\n\n" + ex.Message +
                        "\n\nПодробности записаны в %AppData%\\CustomWin\\error.log",
            "CustomWin", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
