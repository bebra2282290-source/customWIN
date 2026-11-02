using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using CustomWin.Native;
using Microsoft.Win32;

namespace CustomWin.Core;

/// <summary>Обои рабочего стола: картинка, сгенерированный градиент или лёгкое слайд-шоу по таймеру.</summary>
internal sealed class WallpaperService : IDisposable
{
    private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

    private readonly System.Windows.Forms.Timer _timer = new();
    private WallpaperSettings _settings = new();
    private List<string> _queue = new();
    private readonly Random _rng = new();

    /// <summary>Вызывается после смены обоев (нужно «Фону за окнами», если он берёт картинку с рабочего стола).</summary>
    public event Action? WallpaperChanged;

    public WallpaperService() => _timer.Tick += (_, _) => NextSlide();

    public static string? CurrentWallpaperPath()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var path = k?.GetValue("WallPaper") as string;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
        // Windows может хранить копию в TranscodedWallpaper
        var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Themes\TranscodedWallpaper");
        return File.Exists(transcoded) ? transcoded : null;
    }

    /// <param name="setNow">true — поставить обои сейчас; false — только запустить слайд-шоу (при старте программы).</param>
    public void Apply(WallpaperSettings s, bool setNow)
    {
        _settings = s;
        _timer.Stop();

        switch (s.Mode)
        {
            case WallpaperMode.Image when setNow && File.Exists(s.ImagePath):
                Set(s.ImagePath, s.Fit);
                break;
            case WallpaperMode.Gradient when setNow:
                Set(GenerateGradient(s.GradientFrom, s.GradientTo, s.GradientAngle), WallpaperFit.Fill);
                break;
            case WallpaperMode.Slideshow:
                _queue.Clear();
                if (setNow) NextSlide();
                _timer.Interval = s.SlideshowMinutes * 60_000;
                _timer.Start();
                break;
        }
    }

    private void NextSlide()
    {
        if (_queue.Count == 0)
        {
            if (!Directory.Exists(_settings.SlideshowFolder)) return;
            _queue = Directory.EnumerateFiles(_settings.SlideshowFolder)
                .Where(f => ImageExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f).ToList();
            if (_settings.Shuffle) _queue = _queue.OrderBy(_ => _rng.Next()).ToList();
            if (_queue.Count == 0) return;
        }
        var next = _queue[0];
        _queue.RemoveAt(0);
        if (File.Exists(next)) Set(next, _settings.Fit);
    }

    public void Set(string path, WallpaperFit fit)
    {
        var (style, tile) = fit switch
        {
            WallpaperFit.Fit => ("6", "0"),
            WallpaperFit.Stretch => ("2", "0"),
            WallpaperFit.Tile => ("0", "1"),
            WallpaperFit.Center => ("0", "0"),
            WallpaperFit.Span => ("22", "0"),
            _ => ("10", "0"),
        };
        using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop", true))
        {
            k.SetValue("WallpaperStyle", style);
            k.SetValue("TileWallpaper", tile);
        }
        Win32.SystemParametersInfo(Win32.SPI_SETDESKWALLPAPER, 0, path, Win32.SPIF_UPDATEINIFILE | Win32.SPIF_SENDCHANGE);
        WallpaperChanged?.Invoke();
    }

    public static string GenerateGradient(Color from, Color to, int angle)
    {
        var screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        Directory.CreateDirectory(Storage.GeneratedDir);
        var path = Path.Combine(Storage.GeneratedDir,
            $"gradient_{from.ToArgb():X8}_{to.ToArgb():X8}_{angle}_{screen.Width}x{screen.Height}.png");
        if (File.Exists(path)) return path;

        using var bmp = RenderGradient(from, to, angle, screen.Width, screen.Height);
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    public static Bitmap RenderGradient(Color from, Color to, int angle, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        using var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), from, to, angle, true);
        g.FillRectangle(brush, 0, 0, width, height);
        return bmp;
    }

    public void Dispose() => _timer.Dispose();
}
