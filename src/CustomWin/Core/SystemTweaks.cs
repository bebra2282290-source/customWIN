using System.Diagnostics;
using CustomWin.Native;
using Microsoft.Win32;

namespace CustomWin.Core;

/// <summary>Системные настройки, которые хранятся в реестре текущего пользователя (HKCU). Права администратора не нужны.</summary>
internal static class SystemTweaks
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string Dwm = @"Software\Microsoft\Windows\DWM";
    private const string Accent = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string SearchKey = @"Software\Microsoft\Windows\CurrentVersion\Search";
    private const string DesktopKey = @"Control Panel\Desktop";
    private const string ClassicMenuKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

    /// <summary>Применяет тему и цвета. Возвращает true, если для части изменений желателен перезапуск Проводника.</summary>
    public static bool ApplyTheme(ThemeSettings t)
    {
        bool needsExplorer = false;

        if (t.AppsTheme != ThemeMode.Keep) SetDword(Personalize, "AppsUseLightTheme", t.AppsTheme == ThemeMode.Light ? 1 : 0);
        if (t.SystemTheme != ThemeMode.Keep) SetDword(Personalize, "SystemUsesLightTheme", t.SystemTheme == ThemeMode.Light ? 1 : 0);
        if (t.Transparency != Toggle.Keep) SetDword(Personalize, "EnableTransparency", t.Transparency == Toggle.On ? 1 : 0);
        if (t.AccentOnTitleBars != Toggle.Keep) SetDword(Dwm, "ColorPrevalence", t.AccentOnTitleBars == Toggle.On ? 1 : 0);
        if (t.AccentOnTaskbar != Toggle.Keep) SetDword(Personalize, "ColorPrevalence", t.AccentOnTaskbar == Toggle.On ? 1 : 0);

        if (!t.AccentColor.IsEmpty)
        {
            SetAccent(t.AccentColor);
            needsExplorer = true;
        }

        Win32.BroadcastSettingChange("ImmersiveColorSet");
        Win32.BroadcastSettingChange("WindowsThemeElement");
        return needsExplorer;
    }

    private static void SetAccent(Color c)
    {
        static int Abgr(Color x) => unchecked((int)(0xFF000000u | ((uint)x.B << 16) | ((uint)x.G << 8) | x.R));
        static int Argb(Color x, uint a) => unchecked((int)((a << 24) | ((uint)x.R << 16) | ((uint)x.G << 8) | x.B));

        // Палитра из 8 оттенков: 3 светлее, базовый, 3 темнее, служебный
        var shades = new[] { Mix(c, Color.White, .60), Mix(c, Color.White, .40), Mix(c, Color.White, .20), c,
                             Mix(c, Color.Black, .25), Mix(c, Color.Black, .45), Mix(c, Color.Black, .65), Mix(c, Color.Black, .80) };
        var palette = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            palette[i * 4] = shades[i].R; palette[i * 4 + 1] = shades[i].G; palette[i * 4 + 2] = shades[i].B; palette[i * 4 + 3] = 0;
        }

        using (var k = Registry.CurrentUser.CreateSubKey(Accent, true))
        {
            k.SetValue("AccentPalette", palette, RegistryValueKind.Binary);
            k.SetValue("AccentColorMenu", Abgr(c), RegistryValueKind.DWord);
            k.SetValue("StartColorMenu", Abgr(shades[5]), RegistryValueKind.DWord);
        }
        using (var k = Registry.CurrentUser.CreateSubKey(Dwm, true))
        {
            k.SetValue("AccentColor", Abgr(c), RegistryValueKind.DWord);
            k.SetValue("ColorizationColor", Argb(c, 0xC4), RegistryValueKind.DWord);
            k.SetValue("ColorizationAfterglow", Argb(c, 0xC4), RegistryValueKind.DWord);
        }
        // Отключаем «автоматический выбор цвета акцента по обоям»
        using (var k = Registry.CurrentUser.CreateSubKey(DesktopKey, true))
            k.SetValue("AutoColorization", "0", RegistryValueKind.String);
    }

    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    /// <summary>Панель задач, Проводник, анимации. Возвращает true, если нужен перезапуск Проводника.</summary>
    public static bool ApplyShell(ShellSettings s)
    {
        bool needsExplorer = false;

        if (s.TaskbarAlignment != TaskbarAlign.Keep) SetDword(Advanced, "TaskbarAl", s.TaskbarAlignment == TaskbarAlign.Center ? 1 : 0);
        if (s.TaskViewButton != Toggle.Keep) SetDword(Advanced, "ShowTaskViewButton", s.TaskViewButton == Toggle.On ? 1 : 0);
        if (s.Search != SearchBoxMode.Keep) SetDword(SearchKey, "SearchboxTaskbarMode", (int)s.Search);
        if (s.ShowFileExtensions != Toggle.Keep) SetDword(Advanced, "HideFileExt", s.ShowFileExtensions == Toggle.On ? 0 : 1);
        if (s.ShowHiddenFiles != Toggle.Keep) SetDword(Advanced, "Hidden", s.ShowHiddenFiles == Toggle.On ? 1 : 2);
        if (s.OpenExplorerTo != ExplorerStart.Keep) SetDword(Advanced, "LaunchTo", s.OpenExplorerTo == ExplorerStart.ThisPC ? 1 : 2);

        if (s.ClassicContextMenu != Toggle.Keep)
        {
            bool isOn = Registry.CurrentUser.OpenSubKey(ClassicMenuKey + @"\InprocServer32") != null;
            bool wantOn = s.ClassicContextMenu == Toggle.On;
            if (isOn != wantOn)
            {
                if (wantOn)
                {
                    using var k = Registry.CurrentUser.CreateSubKey(ClassicMenuKey + @"\InprocServer32", true);
                    k.SetValue("", "", RegistryValueKind.String);
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(ClassicMenuKey, throwOnMissingSubKey: false);
                }
                needsExplorer = true;
            }
        }

        const uint flags = Win32.SPIF_UPDATEINIFILE | Win32.SPIF_SENDCHANGE;
        if (s.Animations != Toggle.Keep)
        {
            bool on = s.Animations == Toggle.On;
            var info = new ANIMATIONINFO { cbSize = 8, iMinAnimate = on ? 1 : 0 };
            Win32.SystemParametersInfo(Win32.SPI_SETANIMATION, 8, ref info, flags);
            Win32.SystemParametersInfo(Win32.SPI_SETCLIENTAREAANIMATION, 0, new IntPtr(on ? 1 : 0), flags);
        }
        if (s.MenuAnimation != Toggle.Keep)
            Win32.SystemParametersInfo(Win32.SPI_SETMENUANIMATION, 0, new IntPtr(s.MenuAnimation == Toggle.On ? 1 : 0), flags);
        if (s.MenuShowDelay >= 0)
            Win32.SystemParametersInfo(Win32.SPI_SETMENUSHOWDELAY, (uint)s.MenuShowDelay, IntPtr.Zero, flags);

        Win32.BroadcastSettingChange("TraySettings");
        Win32.BroadcastSettingChange("ShellState");
        return needsExplorer;
    }

    public static void RestartExplorer()
    {
        int session = Process.GetCurrentProcess().SessionId;
        foreach (var p in Process.GetProcessesByName("explorer"))
        {
            try { if (p.SessionId == session) p.Kill(); } catch { }
            finally { p.Dispose(); }
        }
        // Обычно Windows сама перезапускает оболочку; если нет — запускаем вручную.
        Thread.Sleep(1500);
        if (!Process.GetProcessesByName("explorer").Any(p => p.SessionId == session))
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }

    public static void SetAutostart(bool enable)
    {
        using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (enable) k.SetValue("CustomWin", $"\"{Environment.ProcessPath}\" --tray");
        else k.DeleteValue("CustomWin", false);
    }

    private static void SetDword(string path, string name, int value)
    {
        using var k = Registry.CurrentUser.CreateSubKey(path, true);
        k.SetValue(name, value, RegistryValueKind.DWord);
    }
}
