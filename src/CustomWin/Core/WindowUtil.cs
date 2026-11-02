using System.Text;
using CustomWin.Native;

namespace CustomWin.Core;

/// <summary>Определяет, к каким окнам можно применять оформление, и кэширует имена процессов.</summary>
internal static class WindowUtil
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow", "TaskListThumbnailWnd", "ForegroundStaging", "MultitaskingViewFrame",
        "TopLevelWindowForOverflowXamlIsland", "NotifyIconOverflowWindow", "Shell_InputSwitchTopLevelWindow",
        "#32768", "tooltips_class32", "SysShadow", "IME", "MSCTFIME UI",
    };

    private static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm.exe", "ShellExperienceHost.exe", "StartMenuExperienceHost.exe", "SearchHost.exe", "SearchApp.exe",
        "TextInputHost.exe", "LockApp.exe", "LogonUI.exe", "csrss.exe", "ShellHost.exe",
    };

    private static readonly Dictionary<uint, string> ProcessNames = new();
    private static readonly uint OwnPid = (uint)Environment.ProcessId;

    public static string ProcessNameOf(IntPtr hwnd, out uint pid)
    {
        Win32.GetWindowThreadProcessId(hwnd, out pid);
        if (ProcessNames.TryGetValue(pid, out var cached)) return cached;
        if (ProcessNames.Count > 512) ProcessNames.Clear();

        string name = "";
        var h = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h != IntPtr.Zero)
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            if (Win32.QueryFullProcessImageName(h, 0, sb, ref size)) name = Path.GetFileName(sb.ToString());
            Win32.CloseHandle(h);
        }
        ProcessNames[pid] = name;
        return name;
    }

    public static HashSet<string> ParseExclusions(string text) =>
        new(text.Split(new[] { ',', ';', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? x : x + ".exe"),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>Обычное окно приложения верхнего уровня с заголовком.</summary>
    public static bool IsAppWindow(IntPtr h, HashSet<string> excluded)
    {
        if (h == IntPtr.Zero || !Win32.IsWindowVisible(h)) return false;
        if (Win32.GetAncestor(h, Win32.GA_ROOT) != h) return false;

        long style = Win32.GetStyle(h);
        long ex = Win32.GetExStyle(h);
        if ((style & Win32.WS_CHILD) != 0) return false;
        if ((style & Win32.WS_CAPTION) != Win32.WS_CAPTION) return false;
        if ((ex & Win32.WS_EX_TOOLWINDOW) != 0 && (ex & Win32.WS_EX_APPWINDOW) == 0) return false;
        if (Win32.IsCloaked(h)) return false;
        if (ShellClasses.Contains(Win32.ClassOf(h))) return false;

        var name = ProcessNameOf(h, out uint pid);
        if (pid == OwnPid || name.Length == 0) return false; // нет доступа (например, окно администратора)
        if (ShellProcesses.Contains(name) || excluded.Contains(name)) return false;
        return true;
    }

    public static bool IsMenu(IntPtr h) => h != IntPtr.Zero && Win32.ClassOf(h) == "#32768";
}

/// <summary>
/// Безопасная полупрозрачность чужих окон через WS_EX_LAYERED.
/// Запоминает исходное состояние и умеет вернуть его обратно.
/// </summary>
internal static class LayeredAlpha
{
    private readonly record struct Original(bool HadLayered, byte Alpha);

    private static readonly Dictionary<IntPtr, Original> Touched = new();

    /// <returns>false, если окно нельзя сделать прозрачным (оно уже управляет прозрачностью само).</returns>
    public static bool Set(IntPtr h, int percent)
    {
        if (percent >= 100) { Restore(h); return true; }
        long ex = Win32.GetExStyle(h);

        if (!Touched.ContainsKey(h))
        {
            bool hadLayered = (ex & Win32.WS_EX_LAYERED) != 0;
            byte alpha = 255;
            if (hadLayered)
            {
                // Окно рисует себя через UpdateLayeredWindow — трогать нельзя, сломаем
                if (!Win32.GetLayeredWindowAttributes(h, out _, out alpha, out uint flags)) return false;
                if ((flags & Win32.LWA_ALPHA) == 0) alpha = 255;
            }
            Touched[h] = new Original(hadLayered, alpha);
            if (Touched.Count > 300) Purge();
        }

        if ((ex & Win32.WS_EX_LAYERED) == 0) Win32.SetExStyle(h, ex | Win32.WS_EX_LAYERED);
        return Win32.SetLayeredWindowAttributes(h, 0, (byte)(percent * 255 / 100), Win32.LWA_ALPHA);
    }

    public static void Restore(IntPtr h)
    {
        if (!Touched.Remove(h, out var o) || !Win32.IsWindow(h)) return;
        if (o.HadLayered)
        {
            Win32.SetLayeredWindowAttributes(h, 0, o.Alpha, Win32.LWA_ALPHA);
        }
        else
        {
            Win32.SetLayeredWindowAttributes(h, 0, 255, Win32.LWA_ALPHA);
            Win32.SetExStyle(h, Win32.GetExStyle(h) & ~Win32.WS_EX_LAYERED);
        }
    }

    public static void RestoreAll()
    {
        foreach (var h in Touched.Keys.ToList()) Restore(h);
    }

    private static void Purge()
    {
        foreach (var h in Touched.Keys.Where(k => !Win32.IsWindow(k)).ToList()) Touched.Remove(h);
    }
}
