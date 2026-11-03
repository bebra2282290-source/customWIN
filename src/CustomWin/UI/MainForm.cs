using System.Drawing.Drawing2D;
using CustomWin.Core;

namespace CustomWin.UI;

internal sealed class MainForm : Form
{
    private readonly TrayContext _ctx;
    private Profile _working;
    private bool _dirty;

    private readonly ListBox _presetList = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly Label _presetTitle = new() { Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI Semibold", 14f) };
    private readonly Label _presetDescription = new() { Dock = DockStyle.Top, Height = 48 };
    private readonly PresetPreview _preview = new() { Dock = DockStyle.Fill };
    private readonly Button _deletePreset = new() { Text = "Удалить", AutoSize = true };

    private readonly PropertyGrid _themeGrid = MakeGrid();
    private readonly PropertyGrid _windowsGrid = MakeGrid();
    private readonly PropertyGrid _backdropGrid = MakeGrid();
    private readonly PropertyGrid _wallpaperGrid = MakeGrid();
    private readonly PropertyGrid _shellGrid = MakeGrid();

    private readonly Label _status = new() { AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(8, 8, 0, 0) };
    private readonly CheckBox _autostart = new() { Text = "Запускать вместе с Windows", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };

    public MainForm(TrayContext ctx)
    {
        _ctx = ctx;
        _working = Json.Clone(ctx.Settings.Current);

        Text = "CustomWin — кастомизация Windows";
        Icon = AppIcon.Get();
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(920, 620);
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
        tabs.TabPages.Add(BuildPresetsTab());
        tabs.TabPages.Add(GridTab("Тема и цвета", _themeGrid));
        tabs.TabPages.Add(GridTab("Окна", _windowsGrid));
        tabs.TabPages.Add(GridTab("Фон за окнами и меню", _backdropGrid));
        tabs.TabPages.Add(GridTab("Обои", _wallpaperGrid));
        tabs.TabPages.Add(GridTab("Панель задач и Проводник", _shellGrid));
        tabs.TabPages.Add(BuildAboutTab());

        Controls.Add(tabs);
        Controls.Add(BuildBottomBar());

        BindGrids();
        FillPresets();
        _autostart.Checked = ctx.Settings.Autostart;
        _autostart.CheckedChanged += (_, _) => _ctx.SetAutostart(_autostart.Checked);
        UpdateStatus();
    }

    private static PropertyGrid MakeGrid()
    {
        var g = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            ToolbarVisible = false,
            HelpVisible = true,
            PropertySort = PropertySort.CategorizedAlphabetical,
        };
        return g;
    }

    private TabPage GridTab(string title, PropertyGrid grid)
    {
        grid.PropertyValueChanged += (_, _) => { _dirty = true; UpdateStatus(); };
        var page = new TabPage(title) { Padding = new Padding(6) };
        page.Controls.Add(grid);
        return page;
    }

    // ------------------------------------------------------------------ вкладка «Пресеты»

    private TabPage BuildPresetsTab()
    {
        var page = new TabPage("Пресеты") { Padding = new Padding(8) };

        // Слева список пресетов, справа превью и кнопки
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        var left = new Panel { Dock = DockStyle.Left, Width = 240, BorderStyle = BorderStyle.FixedSingle };
        page.Controls.Add(right);
        page.Controls.Add(left);
        left.Controls.Add(_presetList);
        _presetList.SelectedIndexChanged += (_, _) => ShowSelectedPreset();
        _presetList.DoubleClick += (_, _) => ApplySelectedPreset();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(MakeButton("Применить пресет", (_, _) => ApplySelectedPreset(), primary: true));
        buttons.Controls.Add(MakeButton("Открыть в редакторе", (_, _) => LoadSelectedIntoEditor()));
        buttons.Controls.Add(MakeButton("Сохранить текущие как пресет…", (_, _) => SaveAsPreset()));
        buttons.Controls.Add(MakeButton("Импорт…", (_, _) => ImportPreset()));
        buttons.Controls.Add(MakeButton("Экспорт…", (_, _) => ExportPreset()));
        _deletePreset.Click += (_, _) => DeletePreset();
        buttons.Controls.Add(_deletePreset);

        right.Controls.Add(_preview);
        right.Controls.Add(_presetDescription);
        right.Controls.Add(_presetTitle);
        right.Controls.Add(buttons);
        return page;
    }

    private static Button MakeButton(string text, EventHandler onClick, bool primary = false)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2) };
        if (primary) b.Font = new Font(b.Font, FontStyle.Bold);
        b.Click += onClick;
        return b;
    }

    private void FillPresets(string? select = null)
    {
        _presetList.BeginUpdate();
        _presetList.Items.Clear();
        foreach (var p in _ctx.Presets) _presetList.Items.Add(p);
        _presetList.EndUpdate();

        select ??= _ctx.Settings.LastPreset;
        int idx = _ctx.Presets.FindIndex(p => p.Name == select);
        _presetList.SelectedIndex = idx >= 0 ? idx : (_presetList.Items.Count > 0 ? 0 : -1);
    }

    private Preset? Selected => _presetList.SelectedItem as Preset;

    private void ShowSelectedPreset()
    {
        var p = Selected;
        _presetTitle.Text = p?.Name ?? "";
        _presetDescription.Text = p?.Description ?? "";
        _deletePreset.Enabled = p is { BuiltIn: false };
        _preview.Profile = p?.Profile;
    }

    private async void ApplySelectedPreset()
    {
        var p = Selected;
        if (p == null) return;
        await RunApply(() => _ctx.ApplyPreset(p));
        RefreshFromSettings();
    }

    private void LoadSelectedIntoEditor()
    {
        if (Selected == null) return;
        _working = Json.Clone(Selected.Profile);
        BindGrids();
        _dirty = true;
        UpdateStatus();
    }

    private void SaveAsPreset()
    {
        var name = InputBox.Show(this, "Новый пресет", "Название пресета:", "Мой пресет");
        if (string.IsNullOrWhiteSpace(name)) return;
        var description = InputBox.Show(this, "Новый пресет", "Короткое описание (необязательно):", "") ?? "";
        var preset = new Preset { Name = name.Trim(), Description = description, Profile = Json.Clone(_working) };
        Storage.SaveUserPreset(preset);
        _ctx.ReloadPresets();
        FillPresets(preset.Name);
    }

    private void ImportPreset()
    {
        using var dlg = new OpenFileDialog { Filter = "Пресет CustomWin (*.json)|*.json", Title = "Импорт пресета" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var p = Storage.TryReadPreset(dlg.FileName);
        if (p == null) { MessageBox.Show(this, "Не удалось прочитать файл пресета.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        p.FilePath = null;
        Storage.SaveUserPreset(p);
        _ctx.ReloadPresets();
        FillPresets(p.Name);
    }

    private void ExportPreset()
    {
        var p = Selected;
        if (p == null) return;
        using var dlg = new SaveFileDialog { Filter = "Пресет CustomWin (*.json)|*.json", FileName = p.Name + ".json", Title = "Экспорт пресета" };
        if (dlg.ShowDialog(this) == DialogResult.OK) Storage.ExportPreset(p, dlg.FileName);
    }

    private void DeletePreset()
    {
        var p = Selected;
        if (p == null || p.BuiltIn || p.FilePath == null) return;
        if (MessageBox.Show(this, $"Удалить пресет «{p.Name}»?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { File.Delete(p.FilePath); } catch { }
        _ctx.ReloadPresets();
        FillPresets();
    }

    // ------------------------------------------------------------------ «О программе»

    private static TabPage BuildAboutTab()
    {
        var page = new TabPage("Что умеет и ограничения") { Padding = new Padding(8) };
        page.Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None, BackColor = SystemColors.Window, Text = AboutText.Value.ReplaceLineEndings("\r\n"),
        });
        return page;
    }

    // ------------------------------------------------------------------ нижняя панель

    private Control BuildBottomBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8, 6, 8, 8), WrapContents = false };
        bar.Controls.Add(MakeButton("Применить", async (_, _) => await RunApply(() => _ctx.ApplyProfile(_working)), primary: true));
        bar.Controls.Add(MakeButton("Отменить изменения", (_, _) => RefreshFromSettings()));
        bar.Controls.Add(MakeButton("Перезапустить Проводник", async (_, _) => await RestartExplorer()));
        bar.Controls.Add(_autostart);
        bar.Controls.Add(_status);
        return bar;
    }

    private async Task RunApply(Func<bool> apply)
    {
        bool needsExplorer;
        try
        {
            Cursor = Cursors.WaitCursor;
            needsExplorer = apply();
        }
        catch (Exception ex)
        {
            _ctx.ReportError(ex);
            MessageBox.Show(this, "Не всё удалось применить: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        finally { Cursor = Cursors.Default; }

        _dirty = false;
        UpdateStatus("Применено ✓");
        if (needsExplorer &&
            MessageBox.Show(this, "Часть изменений (акцентный цвет, контекстное меню, панель задач) вступит в силу после перезапуска Проводника.\n\nПерезапустить сейчас? Открытые папки закроются.",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            await RestartExplorer();
        }
    }

    private async Task RestartExplorer()
    {
        UpdateStatus("Перезапуск Проводника…");
        await _ctx.RestartExplorerAsync();
        UpdateStatus("Проводник перезапущен ✓");
    }

    public void RefreshFromSettings()
    {
        _working = Json.Clone(_ctx.Settings.Current);
        BindGrids();
        _dirty = false;
        FillPresets(_presetList.SelectedItem is Preset p ? p.Name : null);
        UpdateStatus();
    }

    private void BindGrids()
    {
        _themeGrid.SelectedObject = _working.Theme;
        _windowsGrid.SelectedObject = _working.Windows;
        _backdropGrid.SelectedObject = _working.Backdrop;
        _wallpaperGrid.SelectedObject = _working.Wallpaper;
        _shellGrid.SelectedObject = _working.Shell;
    }

    private void UpdateStatus(string? message = null)
    {
        if (message != null) _status.Text = message;
        else if (_dirty) _status.Text = "Есть неприменённые изменения";
        else _status.Text = _ctx.Settings.LastPreset is { } name ? $"Текущий пресет: {name}" : "";
    }
}

/// <summary>Мини-превью пресета: обои + «окно» с цветами заголовка, рамки и акцента.</summary>
internal sealed class PresetPreview : Control
{
    private Profile? _profile;

    public PresetPreview()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    public Profile? Profile { get => _profile; set { _profile = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        if (_profile == null || Width < 40 || Height < 40) return;

        var area = new Rectangle(0, 8, Width - 1, Height - 16);
        var w = _profile.Wallpaper;
        using (var bg = w.Mode == WallpaperMode.Gradient
                   ? new LinearGradientBrush(area, w.GradientFrom, w.GradientTo, w.GradientAngle, true)
                   : new LinearGradientBrush(area, Color.FromArgb(70, 80, 100), Color.FromArgb(40, 45, 60), 90f))
            g.FillRectangle(bg, area);

        bool dark = _profile.Theme.AppsTheme == ThemeMode.Dark || _profile.Windows.TitleBar == TitleBarTheme.Dark;
        var win = new Rectangle(area.X + area.Width / 8, area.Y + area.Height / 7, area.Width * 3 / 5, area.Height * 3 / 5);
        int radius = _profile.Windows.Corners == CornerKind.Square ? 0 : _profile.Windows.Corners == CornerKind.RoundSmall ? 4 : 8;

        var caption = !_profile.Windows.CaptionColor.IsEmpty ? _profile.Windows.CaptionColor : dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
        var text = !_profile.Windows.CaptionTextColor.IsEmpty ? _profile.Windows.CaptionTextColor : dark ? Color.White : Color.Black;
        var body = dark ? Color.FromArgb(28, 28, 28) : Color.White;
        int alpha = _profile.Backdrop.ApplyToWindows ? _profile.Backdrop.WindowOpacity * 255 / 100 : 255;
        if (_profile.Windows.Backdrop is BackdropKind.Acrylic or BackdropKind.Mica or BackdropKind.MicaAlt) alpha = Math.Min(alpha, 225);

        using (var path = Rounded(win, radius))
        {
            using (var b = new SolidBrush(Color.FromArgb(alpha, body))) g.FillPath(b, path);
            var cap = new Rectangle(win.X, win.Y, win.Width, 28);
            g.SetClip(path);
            using (var b = new SolidBrush(Color.FromArgb(Math.Max(alpha, 200), caption))) g.FillRectangle(b, cap);
            g.ResetClip();
            using (var f = new Font("Segoe UI", 9f))
            using (var b = new SolidBrush(text))
                g.DrawString("Окно приложения", f, b, win.X + 10, win.Y + 6);

            var border = _profile.Windows.HideBorder ? Color.Transparent
                : !_profile.Windows.BorderColor.IsEmpty ? _profile.Windows.BorderColor : Color.FromArgb(90, 128, 128, 128);
            using var pen = new Pen(border, 1.5f);
            g.DrawPath(pen, path);
        }

        // Акцентный цвет — «кнопка»
        var accent = _profile.Theme.AccentColor.IsEmpty ? Color.FromArgb(0, 120, 212) : _profile.Theme.AccentColor;
        using (var b = new SolidBrush(accent))
            g.FillRectangle(b, win.X + 14, win.Bottom - 34, 90, 22);

        // Меню
        if (_profile.Backdrop.ApplyToMenus)
        {
            var menu = new Rectangle(win.Right - 30, win.Y + 50, 120, 90);
            using var path = Rounded(menu, 6);
            using var b = new SolidBrush(Color.FromArgb(_profile.Backdrop.MenuOpacity * 255 / 100, dark ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249)));
            g.FillPath(b, path);
            using var t = new SolidBrush(dark ? Color.Gainsboro : Color.DimGray);
            using var f = new Font("Segoe UI", 8.5f);
            string[] items = { "Открыть", "Копировать", "Свойства" };
            for (int i = 0; i < items.Length; i++) g.DrawString(items[i], f, t, menu.X + 10, menu.Y + 8 + i * 24);
        }

        // Панель задач
        var bar = new Rectangle(area.X, area.Bottom - 26, area.Width, 26);
        bool sysDark = _profile.Theme.SystemTheme != ThemeMode.Light;
        var barColor = _profile.Theme.AccentOnTaskbar == Toggle.On ? SystemTweaks.Mix(accent, Color.Black, .45)
            : sysDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(238, 238, 238);
        using (var b = new SolidBrush(Color.FromArgb(_profile.Theme.Transparency == Toggle.Off ? 255 : 215, barColor)))
            g.FillRectangle(b, bar);
        int icons = 5, size = 14, gap = 10;
        int total = icons * size + (icons - 1) * gap;
        int x0 = _profile.Shell.TaskbarAlignment == TaskbarAlign.Left ? bar.X + 12 : bar.X + (bar.Width - total) / 2;
        using (var b = new SolidBrush(sysDark ? Color.FromArgb(200, 255, 255, 255) : Color.FromArgb(160, 0, 0, 0)))
            for (int i = 0; i < icons; i++) g.FillRectangle(b, x0 + i * (size + gap), bar.Y + 6, size, size);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        if (radius <= 0) { p.AddRectangle(r); return p; }
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

internal static class InputBox
{
    public static string? Show(IWin32Window owner, string title, string prompt, string value)
    {
        using var form = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
            AutoScaleDimensions = new SizeF(96f, 96f), AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(380, 120), Font = new Font("Segoe UI", 9.5f),
        };
        var label = new Label { Text = prompt, Left = 12, Top = 12, AutoSize = true };
        var box = new TextBox { Text = value, Left = 12, Top = 38, Width = 356 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 212, Top = 78, Width = 75 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 293, Top = 78, Width = 75 };
        form.Controls.AddRange(new Control[] { label, box, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
    }
}
