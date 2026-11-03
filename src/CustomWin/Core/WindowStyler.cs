using CustomWin.Native;

namespace CustomWin.Core;

/// <summary>
/// Оформление окон других приложений через DWM: тёмный заголовок, цвета заголовка/рамки, углы,
/// материалы Mica/Acrylic и общая полупрозрачность. Работает «снаружи» (без внедрения DLL).
/// </summary>
internal sealed class WindowStyler
{
    private WindowSettings _s = new();
    private HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<IntPtr> _styled = new();

    public bool Active => _s.IsActive();
    public HashSet<string> Excluded => _excluded;

    /// <summary>Непрозрачность, которую окно должно иметь «в покое» (без фона за окном).</summary>
    public int BaseOpacity => Active ? _s.Opacity : 100;

    public void Update(WindowSettings settings)
    {
        bool wasActive = Active;
        var old = _s;
        _s = Json.Clone(settings);
        _excluded = WindowUtil.ParseExclusions(_s.ExcludedProcesses);

        // Сбросить то, что выключили
        if (wasActive && (!Active || NeedsReset(old, _s))) ResetAll(old);
        if (Active) ApplyToAll();
    }

    private static bool NeedsReset(WindowSettings a, WindowSettings b) =>
        (a.CaptionColor != b.CaptionColor && b.CaptionColor.IsEmpty) ||
        (a.CaptionTextColor != b.CaptionTextColor && b.CaptionTextColor.IsEmpty) ||
        (a.BorderColor != b.BorderColor && b.BorderColor.IsEmpty) ||
        (a.HideBorder && !b.HideBorder) ||
        (a.Corners != b.Corners && b.Corners == CornerKind.Default) ||
        (a.Backdrop != b.Backdrop && b.Backdrop == BackdropKind.Default) ||
        (a.ExtendIntoClientArea && !b.ExtendIntoClientArea) ||
        (a.Opacity < 100 && b.Opacity == 100) ||
        a.ExcludedProcesses != b.ExcludedProcesses;

    public void ApplyToAll()
    {
        Win32.EnumWindows((h, _) =>
        {
            if (WindowUtil.IsAppWindow(h, _excluded)) Apply(h);
            return true;
        }, IntPtr.Zero);
    }

    /// <summary>Новое окно появилось или стало активным.</summary>
    public void OnWindow(IntPtr h, bool force = false)
    {
        if (!Active) return;
        if (!force && _styled.Contains(h)) return;
        if (WindowUtil.IsAppWindow(h, _excluded)) Apply(h);
    }

    private void Apply(IntPtr h)
    {
        _styled.Add(h);
        if (_styled.Count > 400) _styled.RemoveWhere(x => !Win32.IsWindow(x));

        if (_s.HasDwmEffects())
        {
            if (_s.TitleBar != TitleBarTheme.Default)
            {
                int dark = _s.TitleBar == TitleBarTheme.Dark ? 1 : 0;
                if (Win32.Dwm(h, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, dark) != 0)
                    Win32.Dwm(h, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, dark);
            }

            if (OsInfo.IsWin11)
            {
                if (_s.Corners != CornerKind.Default) Win32.Dwm(h, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, (int)_s.Corners);
                if (_s.HideBorder) Win32.Dwm(h, Win32.DWMWA_BORDER_COLOR, Win32.DWMWA_COLOR_NONE);
                else if (!_s.BorderColor.IsEmpty) Win32.Dwm(h, Win32.DWMWA_BORDER_COLOR, ColorRef(_s.BorderColor));
                if (!_s.CaptionColor.IsEmpty) Win32.Dwm(h, Win32.DWMWA_CAPTION_COLOR, ColorRef(_s.CaptionColor));
                if (!_s.CaptionTextColor.IsEmpty) Win32.Dwm(h, Win32.DWMWA_TEXT_COLOR, ColorRef(_s.CaptionTextColor));
            }

            if (OsInfo.SupportsBackdrop && _s.Backdrop != BackdropKind.Default)
            {
                Win32.Dwm(h, Win32.DWMWA_SYSTEMBACKDROP_TYPE, (int)_s.Backdrop);
                if (_s.ExtendIntoClientArea)
                {
                    var m = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                    Win32.DwmExtendFrameIntoClientArea(h, ref m);
                }
            }

            // Перерисовать рамку, чтобы изменения были видны сразу
            Win32.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
        }

        if (_s.Opacity < 100) LayeredAlpha.Set(h, _s.Opacity);
    }

    public void ResetAll() => ResetAll(_s);

    /// <summary>Возвращает окна к исходному виду. Сбрасываем только то, что сами меняли.</summary>
    private void ResetAll(WindowSettings was)
    {
        foreach (var h in _styled)
        {
            if (!Win32.IsWindow(h)) continue;
            if (OsInfo.IsWin11)
            {
                if (was.Corners != CornerKind.Default) Win32.Dwm(h, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, 0);
                if (was.HideBorder || !was.BorderColor.IsEmpty) Win32.Dwm(h, Win32.DWMWA_BORDER_COLOR, Win32.DWMWA_COLOR_DEFAULT);
                if (!was.CaptionColor.IsEmpty) Win32.Dwm(h, Win32.DWMWA_CAPTION_COLOR, Win32.DWMWA_COLOR_DEFAULT);
                if (!was.CaptionTextColor.IsEmpty) Win32.Dwm(h, Win32.DWMWA_TEXT_COLOR, Win32.DWMWA_COLOR_DEFAULT);
            }
            if (OsInfo.SupportsBackdrop && was.Backdrop != BackdropKind.Default)
                Win32.Dwm(h, Win32.DWMWA_SYSTEMBACKDROP_TYPE, 0);
            if (was.ExtendIntoClientArea)
            {
                var m = new MARGINS();
                Win32.DwmExtendFrameIntoClientArea(h, ref m);
            }
            LayeredAlpha.Restore(h);
            Win32.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
        }
        _styled.Clear();
    }

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}
