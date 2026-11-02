using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CustomWin.UI;

/// <summary>Иконка рисуется кодом — не нужны файлы ресурсов.</summary>
internal static class AppIcon
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    private static Icon? _icon;

    public static Icon Get()
    {
        if (_icon != null) return _icon;
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(new Rectangle(1, 1, 30, 30), 8);
            using var brush = new LinearGradientBrush(new Rectangle(0, 0, 32, 32),
                Color.FromArgb(0x7B, 0x2F, 0xF7), Color.FromArgb(0x00, 0xC6, 0xFF), 45f);
            g.FillPath(brush, path);
            // «окно» с заголовком
            using var white = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
            g.FillRectangle(white, 7, 9, 18, 3);
            using var glass = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
            g.FillRectangle(glass, 7, 13, 18, 10);
        }
        var h = bmp.GetHicon();
        using (var tmp = Icon.FromHandle(h)) _icon = (Icon)tmp.Clone();
        DestroyIcon(h);
        return _icon;
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
