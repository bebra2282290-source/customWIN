namespace CustomWin.Core;

internal static class BuiltInPresets
{
    private static Color C(string hex) => ColorTranslator.FromHtml(hex);

    public static List<Preset> All() => new()
    {
        Make("Тёмный минимализм", "Тёмная тема, синий акцент, Mica в окнах, спокойный градиент на рабочем столе.", p =>
        {
            p.Theme.AppsTheme = ThemeMode.Dark; p.Theme.SystemTheme = ThemeMode.Dark;
            p.Theme.Transparency = Toggle.On; p.Theme.AccentColor = C("#4F8CFF");
            p.Theme.AccentOnTitleBars = Toggle.Off; p.Theme.AccentOnTaskbar = Toggle.Off;
            p.Windows.Enabled = true; p.Windows.TitleBar = TitleBarTheme.Dark; p.Windows.Backdrop = BackdropKind.Mica;
            p.Windows.Corners = CornerKind.Round;
            Gradient(p, "#0F1020", "#1E2A44", 135);
        }),

        Make("Светлый воздух", "Светлая тема, Mica Alt, мягкий голубой градиент.", p =>
        {
            p.Theme.AppsTheme = ThemeMode.Light; p.Theme.SystemTheme = ThemeMode.Light;
            p.Theme.Transparency = Toggle.On; p.Theme.AccentColor = C("#2B7DE9");
            p.Theme.AccentOnTitleBars = Toggle.Off; p.Theme.AccentOnTaskbar = Toggle.Off;
            p.Windows.Enabled = true; p.Windows.TitleBar = TitleBarTheme.Light; p.Windows.Backdrop = BackdropKind.MicaAlt;
            Gradient(p, "#E8EEF7", "#C9D6EA", 160);
        }),

        Make("Неон", "Фиолетово-розовый неон: цветные рамки и заголовки, обои просвечивают сквозь окна и меню.", p =>
        {
            p.Theme.AppsTheme = ThemeMode.Dark; p.Theme.SystemTheme = ThemeMode.Dark;
            p.Theme.Transparency = Toggle.On; p.Theme.AccentColor = C("#FF2BD6");
            p.Theme.AccentOnTaskbar = Toggle.On;
            p.Windows.Enabled = true; p.Windows.TitleBar = TitleBarTheme.Dark;
            p.Windows.CaptionColor = C("#14061F"); p.Windows.CaptionTextColor = C("#FF9CF0");
            p.Windows.BorderColor = C("#FF2BD6"); p.Windows.Backdrop = BackdropKind.Acrylic;
            Gradient(p, "#12002B", "#3A0CA3", 45);
            p.Backdrop.ApplyToWindows = true; p.Backdrop.ApplyToMenus = true;
            p.Backdrop.Source = BackdropSource.Desktop; p.Backdrop.Dim = 25;
            p.Backdrop.WindowOpacity = 90; p.Backdrop.MenuOpacity = 82;
            p.Shell.ClassicContextMenu = Toggle.On; p.Shell.MenuAnimation = Toggle.Off;
        }),

        Make("Стекло", "Максимум прозрачности: акрил на всё окно, размытая картинка за окнами и меню.", p =>
        {
            p.Theme.AppsTheme = ThemeMode.Dark; p.Theme.SystemTheme = ThemeMode.Dark;
            p.Theme.Transparency = Toggle.On; p.Theme.AccentColor = C("#7FD1FF");
            p.Windows.Enabled = true; p.Windows.TitleBar = TitleBarTheme.Dark;
            p.Windows.Backdrop = BackdropKind.Acrylic; p.Windows.ExtendIntoClientArea = true;
            p.Windows.HideBorder = true;
            Gradient(p, "#0B486B", "#3B8686", 120);
            p.Backdrop.ApplyToWindows = true; p.Backdrop.ApplyToMenus = true;
            p.Backdrop.Blur = 12; p.Backdrop.Dim = 20;
            p.Backdrop.WindowOpacity = 86; p.Backdrop.MenuOpacity = 78;
            p.Shell.ClassicContextMenu = Toggle.On; p.Shell.MenuAnimation = Toggle.Off;
        }),

        Make("Закат", "Тёплые оранжевые тона, акцентные рамки, фон за меню.", p =>
        {
            p.Theme.AppsTheme = ThemeMode.Dark; p.Theme.SystemTheme = ThemeMode.Dark;
            p.Theme.Transparency = Toggle.On; p.Theme.AccentColor = C("#FF7A45");
            p.Windows.Enabled = true; p.Windows.TitleBar = TitleBarTheme.Dark;
            p.Windows.CaptionColor = C("#2A1410"); p.Windows.CaptionTextColor = C("#FFD3B6");
            p.Windows.BorderColor = C("#FF7A45");
            Gradient(p, "#FF5F6D", "#FFC371", 135);
            p.Backdrop.ApplyToMenus = true; p.Backdrop.Blur = 6; p.Backdrop.Dim = 35; p.Backdrop.MenuOpacity = 85;
            p.Shell.ClassicContextMenu = Toggle.On; p.Shell.MenuAnimation = Toggle.Off;
        }),

        Make("Ретро", "Квадратные углы, классические цвета заголовков, иконки слева, без анимаций.", p =>
        {
            p.Theme.AppsTheme = ThemeMode.Light; p.Theme.SystemTheme = ThemeMode.Dark;
            p.Theme.Transparency = Toggle.Off; p.Theme.AccentColor = C("#008080");
            p.Windows.Enabled = true; p.Windows.Corners = CornerKind.Square;
            p.Windows.CaptionColor = C("#000080"); p.Windows.CaptionTextColor = C("#FFFFFF");
            p.Windows.BorderColor = C("#000080"); p.Windows.Backdrop = BackdropKind.None;
            Gradient(p, "#008080", "#005F5F", 90);
            p.Shell.TaskbarAlignment = TaskbarAlign.Left; p.Shell.ClassicContextMenu = Toggle.On;
            p.Shell.ShowFileExtensions = Toggle.On; p.Shell.Animations = Toggle.Off;
            p.Shell.MenuAnimation = Toggle.Off; p.Shell.MenuShowDelay = 0;
        }),

        Make("Производительность", "Для слабых ПК: без прозрачности и анимаций, мгновенные меню, никаких фоновых эффектов.", p =>
        {
            p.Theme.Transparency = Toggle.Off;
            p.Windows.Enabled = false; p.Backdrop.ApplyToWindows = false; p.Backdrop.ApplyToMenus = false;
            p.Shell.Animations = Toggle.Off; p.Shell.MenuAnimation = Toggle.Off; p.Shell.MenuShowDelay = 0;
            p.Shell.ShowFileExtensions = Toggle.On;
        }),

        Make("Стандарт Windows", "Вернуть всё как было по умолчанию в Windows 11 (обои не трогаются).", p =>
        {
            p.Theme.AppsTheme = ThemeMode.Light; p.Theme.SystemTheme = ThemeMode.Dark;
            p.Theme.Transparency = Toggle.On;
            p.Theme.AccentOnTitleBars = Toggle.Off; p.Theme.AccentOnTaskbar = Toggle.Off;
            p.Windows.Enabled = false; p.Backdrop.ApplyToWindows = false; p.Backdrop.ApplyToMenus = false;
            p.Shell.TaskbarAlignment = TaskbarAlign.Center; p.Shell.TaskViewButton = Toggle.On;
            p.Shell.ClassicContextMenu = Toggle.Off; p.Shell.Animations = Toggle.On;
            p.Shell.MenuAnimation = Toggle.On; p.Shell.MenuShowDelay = 400;
        }),
    };

    private static void Gradient(Profile p, string from, string to, int angle)
    {
        p.Wallpaper.Mode = WallpaperMode.Gradient;
        p.Wallpaper.GradientFrom = C(from);
        p.Wallpaper.GradientTo = C(to);
        p.Wallpaper.GradientAngle = angle;
        p.Backdrop.GradientFrom = C(from);
        p.Backdrop.GradientTo = C(to);
    }

    private static Preset Make(string name, string description, Action<Profile> setup)
    {
        var p = new Profile();
        setup(p);
        return new Preset { Name = name, Description = description, Profile = p, BuiltIn = true };
    }
}
