using CustomWin.Native;
using Microsoft.Win32;

namespace CustomWin.Core;

/// <summary>
/// Ядро: применяет профиль и слушает системные события окон.
/// Хуки ставятся только когда они реально нужны — с выключенными эффектами программа
/// просто висит в трее и ничего не делает.
/// </summary>
internal sealed class Customizer : IDisposable
{
    public WindowStyler Styler { get; } = new();
    public BackdropManager Backdrop { get; }
    public WallpaperService Wallpaper { get; } = new();

    private readonly Win32.WinEventDelegate _callback; // держим ссылку, иначе GC соберёт делегат
    private IntPtr _hookSystem, _hookObject, _hookLocation;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    public Customizer()
    {
        Backdrop = new BackdropManager(Styler);
        _callback = OnWinEvent;
        Wallpaper.WallpaperChanged += Backdrop.OnWallpaperChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
    }

    /// <param name="system">true — записать системные настройки (тема, панель задач, обои). false — только запустить фоновые эффекты (при старте).</param>
    /// <returns>true, если желательно перезапустить Проводник.</returns>
    public bool Apply(Profile p, bool system)
    {
        bool needsExplorer = false;
        if (system)
        {
            needsExplorer |= SystemTweaks.ApplyTheme(p.Theme);
            needsExplorer |= SystemTweaks.ApplyShell(p.Shell);
        }
        Wallpaper.Apply(p.Wallpaper, setNow: system);
        Styler.Update(p.Windows);
        Backdrop.Update(p.Backdrop);
        UpdateHooks();
        return needsExplorer;
    }

    private void UpdateHooks()
    {
        bool any = Styler.Active || Backdrop.Active;
        SetHook(ref _hookSystem, any, Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_MINIMIZEEND);
        SetHook(ref _hookObject, any, Win32.EVENT_OBJECT_SHOW, Win32.EVENT_OBJECT_HIDE);
        SetHook(ref _hookLocation, Backdrop.Active, Win32.EVENT_OBJECT_LOCATIONCHANGE, Win32.EVENT_OBJECT_LOCATIONCHANGE);
    }

    private void SetHook(ref IntPtr hook, bool enable, uint min, uint max)
    {
        if (enable && hook == IntPtr.Zero)
            hook = Win32.SetWinEventHook(min, max, IntPtr.Zero, _callback, 0, 0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
        else if (!enable && hook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(hook);
            hook = IntPtr.Zero;
        }
    }

    private void OnWinEvent(IntPtr hook, uint type, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || idObject != Win32.OBJID_WINDOW || idChild != Win32.CHILDID_SELF) return;
        try
        {
            switch (type)
            {
                case Win32.EVENT_OBJECT_LOCATIONCHANGE:
                    Backdrop.OnLocationChange(hwnd);
                    break;
                case Win32.EVENT_SYSTEM_FOREGROUND:
                    Styler.OnWindow(hwnd);
                    Backdrop.OnForeground(hwnd);
                    break;
                case Win32.EVENT_OBJECT_SHOW:
                    Styler.OnWindow(hwnd);
                    Backdrop.OnMenuShown(hwnd);
                    break;
                case Win32.EVENT_SYSTEM_MENUPOPUPSTART:
                    Backdrop.OnMenuShown(hwnd);
                    break;
                case Win32.EVENT_OBJECT_HIDE:
                case Win32.EVENT_SYSTEM_MENUPOPUPEND:
                    Backdrop.OnHidden(hwnd);
                    break;
                case Win32.EVENT_SYSTEM_MINIMIZESTART:
                    Backdrop.OnMinimize(hwnd);
                    break;
                case Win32.EVENT_SYSTEM_MINIMIZEEND:
                case Win32.EVENT_SYSTEM_MOVESIZEEND:
                    Backdrop.OnForeground(hwnd);
                    break;
            }
        }
        catch
        {
            // Окно могло исчезнуть между событием и обработкой — это нормально
        }
    }

    private void OnDisplayChanged(object? sender, EventArgs e)
    {
        if (_ui != null) _ui.Post(_ => Backdrop.OnDisplayChanged(), null);
        else Backdrop.OnDisplayChanged();
    }

    /// <summary>Вернуть все окна в исходный вид (при выходе).</summary>
    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SetHook(ref _hookSystem, false, 0, 0);
        SetHook(ref _hookObject, false, 0, 0);
        SetHook(ref _hookLocation, false, 0, 0);
        Backdrop.Dispose();
        Styler.ResetAll();
        LayeredAlpha.RestoreAll();
        Wallpaper.Dispose();
    }
}
