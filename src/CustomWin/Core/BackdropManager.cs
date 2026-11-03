using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using CustomWin.Native;

namespace CustomWin.Core;

/// <summary>
/// «Обои внутри окон и меню».
/// Windows не умеет ставить картинку фоном в чужие окна и меню, поэтому делаем так:
/// позади активного окна (или открытого меню) ставим своё окно-подложку с картинкой
/// ровно по его границам, а само окно делаем полупрозрачным. Подложка не принимает
/// клики и не перехватывает фокус. Внедрения в чужие процессы нет.
/// </summary>
internal sealed class BackdropManager : IDisposable
{
    private readonly WindowStyler _styler;
    private BackdropSettings _s = new();
    private Bitmap? _image;
    private string _imageKey = "";
    private float _scale = 1f;
    private Rectangle _virtual;

    private OverlayForm? _windowOverlay;
    private IntPtr _target;

    private readonly Dictionary<IntPtr, OverlayForm> _menus = new();
    private readonly Stack<OverlayForm> _pool = new();

    private readonly System.Windows.Forms.Timer _zOrderTimer = new() { Interval = 400 };
    private readonly System.Windows.Forms.Timer _menuTimer = new() { Interval = 120 };

    public BackdropManager(WindowStyler styler)
    {
        _styler = styler;
        _zOrderTimer.Tick += (_, _) => CheckZOrder();
        _menuTimer.Tick += (_, _) => RefreshMenus();
    }

    public bool Active => _s.IsActive();
    public bool TracksWindows => _s.ApplyToWindows;

    public void Update(BackdropSettings settings)
    {
        _s = Json.Clone(settings);
        if (!Active) { Shutdown(); return; }

        RebuildImage(force: false);

        if (_s.ApplyToWindows)
        {
            if (_target != IntPtr.Zero) LayeredAlpha.Set(_target, _s.WindowOpacity);
            OnForeground(Win32.GetForegroundWindow());
            _windowOverlay?.Invalidate();
            _zOrderTimer.Start();
        }
        else
        {
            DetachWindow();
            _zOrderTimer.Stop();
        }

        if (!_s.ApplyToMenus) CloseAllMenus();
    }

    // ------------------------------------------------------------------ картинка

    public void OnWallpaperChanged()
    {
        if (Active && _s.Source == BackdropSource.Desktop) RebuildImage(force: true);
    }

    public void OnDisplayChanged()
    {
        if (Active) RebuildImage(force: true);
    }

    private void RebuildImage(bool force)
    {
        _virtual = SystemInformation.VirtualScreen;
        // Размытую или растягиваемую картинку храним в половинном разрешении — в 4 раза меньше памяти
        _scale = _s.Blur > 0 || _s.Placement == BackdropPlacement.Window ? 0.5f : 1f;
        int w = Math.Max(1, (int)(_virtual.Width * _scale));
        int h = Math.Max(1, (int)(_virtual.Height * _scale));

        string sourcePath = _s.Source switch
        {
            BackdropSource.Desktop => WallpaperService.CurrentWallpaperPath() ?? "",
            BackdropSource.Image => _s.ImagePath,
            _ => "",
        };
        long stamp = File.Exists(sourcePath) ? File.GetLastWriteTimeUtc(sourcePath).Ticks : 0;
        string key = $"{_s.Source}|{sourcePath}|{stamp}|{_s.GradientFrom.ToArgb()}|{_s.GradientTo.ToArgb()}|" +
                     $"{_s.Blur}|{_s.Dim}|{_s.Tint.ToArgb()}|{w}x{h}";
        if (!force && key == _imageKey && _image != null) return;

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (_s.Source == BackdropSource.Gradient)
            {
                using var brush = new LinearGradientBrush(new Rectangle(0, 0, w, h), _s.GradientFrom, _s.GradientTo, 135f, true);
                g.FillRectangle(brush, 0, 0, w, h);
            }
            else if (File.Exists(sourcePath))
            {
                try
                {
                    using var fs = File.OpenRead(sourcePath);
                    using var src = Image.FromStream(fs, false, false);
                    DrawCover(g, src, w, h);
                }
                catch { g.Clear(Color.FromArgb(32, 32, 40)); }
            }
            else
            {
                g.Clear(Color.FromArgb(32, 32, 40));
            }
        }

        if (_s.Blur > 0) Blur(bmp, _s.Blur);

        using (var g = Graphics.FromImage(bmp))
        {
            if (!_s.Tint.IsEmpty)
                using (var b = new SolidBrush(Color.FromArgb(90, _s.Tint))) g.FillRectangle(b, 0, 0, w, h);
            if (_s.Dim > 0)
                using (var b = new SolidBrush(Color.FromArgb(_s.Dim * 255 / 100, Color.Black))) g.FillRectangle(b, 0, 0, w, h);
        }

        var old = _image;
        _image = bmp;
        _imageKey = key;
        old?.Dispose();

        _windowOverlay?.Invalidate();
        foreach (var o in _menus.Values) o.Invalidate();
    }

    private static void DrawCover(Graphics g, Image src, int w, int h)
    {
        float scale = Math.Max((float)w / src.Width, (float)h / src.Height);
        float dw = src.Width * scale, dh = src.Height * scale;
        g.DrawImage(src, (w - dw) / 2f, (h - dh) / 2f, dw, dh);
    }

    /// <summary>Дешёвое «матовое стекло»: уменьшить и снова увеличить с билинейной фильтрацией.</summary>
    private static void Blur(Bitmap bmp, int amount)
    {
        int f = 2 + amount * 2;
        int sw = Math.Max(1, bmp.Width / f), sh = Math.Max(1, bmp.Height / f);
        using var small = new Bitmap(sw, sh, PixelFormat.Format32bppPArgb);
        using var attrs = new ImageAttributes();
        attrs.SetWrapMode(WrapMode.TileFlipXY); // без тёмных краёв
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(bmp, new Rectangle(0, 0, sw, sh), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attrs);
        }
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(small, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, sw, sh, GraphicsUnit.Pixel, attrs);
        }
    }

    internal void Paint(OverlayForm form, Graphics g)
    {
        var r = form.ScreenRect;
        if (_image == null || r.Width <= 0 || r.Height <= 0) { g.Clear(Color.Black); return; }

        Rectangle src;
        if (_s.Placement == BackdropPlacement.Screen)
        {
            src = new Rectangle((int)((r.Left - _virtual.X) * _scale), (int)((r.Top - _virtual.Y) * _scale),
                                (int)(r.Width * _scale), (int)(r.Height * _scale));
        }
        else
        {
            // «cover»: вырезаем из картинки кусок с пропорциями окна
            float ar = (float)r.Width / r.Height;
            int iw = _image.Width, ih = _image.Height;
            int cw = iw, ch = (int)(iw / ar);
            if (ch > ih) { ch = ih; cw = (int)(ih * ar); }
            src = new Rectangle((iw - cw) / 2, (ih - ch) / 2, cw, ch);
        }

        bool oneToOne = _scale == 1f && _s.Placement == BackdropPlacement.Screen;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = oneToOne ? InterpolationMode.NearestNeighbor : InterpolationMode.Bilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_image, new Rectangle(0, 0, r.Width, r.Height), src, GraphicsUnit.Pixel);
    }

    // ------------------------------------------------------------------ окна

    public void OnForeground(IntPtr h)
    {
        if (!_s.ApplyToWindows || h == IntPtr.Zero) return;
        if (h == _target) { Reposition(); return; }
        if (h == _windowOverlay?.Handle || _menus.ContainsKey(h)) return;
        if (!WindowUtil.IsAppWindow(h, _styler.Excluded)) return;
        AttachWindow(h);
    }

    public void OnLocationChange(IntPtr h)
    {
        if (h == _target) Reposition();
        else if (_menus.ContainsKey(h)) PlaceMenu(h);
    }

    public void OnMinimize(IntPtr h)
    {
        if (h == _target) _windowOverlay?.HideOverlay();
    }

    public void OnHidden(IntPtr h)
    {
        if (h == _target) DetachWindow();
        else if (_menus.ContainsKey(h)) ReleaseMenu(h);
    }

    private void AttachWindow(IntPtr h)
    {
        var previous = _target;
        _target = IntPtr.Zero;
        if (previous != IntPtr.Zero && Win32.IsWindow(previous)) LayeredAlpha.Set(previous, _styler.BaseOpacity);

        if (!LayeredAlpha.Set(h, _s.WindowOpacity)) { _windowOverlay?.HideOverlay(); return; }
        _target = h;
        Reposition();
    }

    private void DetachWindow()
    {
        if (_target != IntPtr.Zero && Win32.IsWindow(_target)) LayeredAlpha.Set(_target, _styler.BaseOpacity);
        _target = IntPtr.Zero;
        _windowOverlay?.HideOverlay();
    }

    private void Reposition()
    {
        if (_target == IntPtr.Zero) return;
        if (!Win32.IsWindow(_target)) { DetachWindow(); return; }
        if (!Win32.IsWindowVisible(_target) || Win32.IsIconic(_target)) { _windowOverlay?.HideOverlay(); return; }
        if (!Win32.TryGetVisibleBounds(_target, out var r)) return;

        _windowOverlay ??= new OverlayForm(this, CornerKind.Round);
        bool topmost = (Win32.GetExStyle(_target) & Win32.WS_EX_TOPMOST) != 0;
        _windowOverlay.PlaceUnder(_target, r, topmost);
    }

    private void CheckZOrder()
    {
        if (_target == IntPtr.Zero || _windowOverlay == null) return;
        if (!Win32.IsWindow(_target)) { DetachWindow(); return; }
        if (!Win32.IsWindowVisible(_target) || Win32.IsIconic(_target)) { _windowOverlay.HideOverlay(); return; }
        if (!_windowOverlay.IsShown || Win32.GetWindow(_target, Win32.GW_HWNDNEXT) != _windowOverlay.Handle)
            Reposition();
    }

    // ------------------------------------------------------------------ меню

    public void OnMenuShown(IntPtr h)
    {
        if (!_s.ApplyToMenus || !WindowUtil.IsMenu(h)) return;
        if (!_menus.ContainsKey(h))
        {
            if (!LayeredAlpha.Set(h, _s.MenuOpacity)) return;
            var overlay = _pool.Count > 0 ? _pool.Pop() : new OverlayForm(this, CornerKind.RoundSmall);
            _menus[h] = overlay;
        }
        PlaceMenu(h);
        _menuTimer.Start();
    }

    private void PlaceMenu(IntPtr h)
    {
        if (!_menus.TryGetValue(h, out var overlay)) return;
        if (!Win32.IsWindowVisible(h) || !Win32.TryGetVisibleBounds(h, out var r)) return;
        overlay.PlaceUnder(h, r, topmost: true);
    }

    /// <summary>Анимация меню может сбросить прозрачность — проверяем, пока меню открыты.</summary>
    private void RefreshMenus()
    {
        foreach (var h in _menus.Keys.ToList())
        {
            if (!Win32.IsWindow(h) || !Win32.IsWindowVisible(h)) { ReleaseMenu(h); continue; }
            LayeredAlpha.Set(h, _s.MenuOpacity);
            PlaceMenu(h);
        }
        if (_menus.Count == 0) _menuTimer.Stop();
    }

    private void ReleaseMenu(IntPtr h)
    {
        if (!_menus.Remove(h, out var overlay)) return;
        LayeredAlpha.Restore(h);
        overlay.HideOverlay();
        if (_pool.Count < 4) _pool.Push(overlay); else overlay.Dispose();
    }

    private void CloseAllMenus()
    {
        foreach (var h in _menus.Keys.ToList()) ReleaseMenu(h);
        _menuTimer.Stop();
    }

    // ------------------------------------------------------------------ завершение

    private void Shutdown()
    {
        _zOrderTimer.Stop();
        DetachWindow();
        CloseAllMenus();
        _windowOverlay?.Dispose();
        _windowOverlay = null;
        while (_pool.Count > 0) _pool.Pop().Dispose();
        _image?.Dispose();
        _image = null;
        _imageKey = "";
    }

    public void Dispose()
    {
        Shutdown();
        _zOrderTimer.Dispose();
        _menuTimer.Dispose();
    }
}

/// <summary>Окно-подложка: не активируется, пропускает клики, не видно на панели задач.</summary>
internal sealed class OverlayForm : Form
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int WM_DPICHANGED = 0x02E0;
    private const int HTTRANSPARENT = -1;
    private const int MA_NOACTIVATE = 3;

    private readonly BackdropManager _owner;
    private readonly CornerKind _corners;

    public RECT ScreenRect { get; private set; }
    public bool IsShown { get; private set; }

    public OverlayForm(BackdropManager owner, CornerKind corners)
    {
        _owner = owner;
        _corners = corners;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        Text = "";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque |
                 ControlStyles.OptimizedDoubleBuffer, true);
        _ = Handle; // создаём окно сразу, показываем через SetWindowPos
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= (int)(Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_LAYERED);
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Win32.SetLayeredWindowAttributes(Handle, 0, 255, Win32.LWA_ALPHA);
        if (OsInfo.IsWin11) Win32.Dwm(Handle, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, (int)_corners);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_NCHITTEST: m.Result = new IntPtr(HTTRANSPARENT); return;
            case WM_MOUSEACTIVATE: m.Result = new IntPtr(MA_NOACTIVATE); return;
            case WM_DPICHANGED: m.Result = IntPtr.Zero; return; // размер задаём сами
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e) => _owner.Paint(this, e.Graphics);

    public void PlaceUnder(IntPtr target, RECT r, bool topmost)
    {
        bool changed = r.Left != ScreenRect.Left || r.Top != ScreenRect.Top || r.Width != ScreenRect.Width || r.Height != ScreenRect.Height;
        ScreenRect = r;

        bool isTopmost = (Win32.GetExStyle(Handle) & Win32.WS_EX_TOPMOST) != 0;
        if (topmost && !isTopmost)
            Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        else if (!topmost && isTopmost)
            Win32.SetWindowPos(Handle, Win32.HWND_NOTOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

        Win32.SetWindowPos(Handle, target, r.Left, r.Top, r.Width, r.Height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW | Win32.SWP_NOOWNERZORDER);
        IsShown = true;
        if (changed) Invalidate();
    }

    public void HideOverlay()
    {
        if (!IsShown) return;
        Win32.ShowWindow(Handle, Win32.SW_HIDE);
        IsShown = false;
    }
}
