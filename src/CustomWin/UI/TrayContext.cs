using System.Diagnostics;
using CustomWin.Core;
using CustomWin.Native;

namespace CustomWin.UI;

/// <summary>Живёт в трее. Окно настроек создаётся только когда его открывают и уничтожается при закрытии — так программа почти не ест память.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly SynchronizationContext _ui;
    private readonly Customizer _engine;
    private readonly NotifyIcon _tray;
    private readonly EventWaitHandle _showEvent;
    private readonly RegisteredWaitHandle _showWait;
    private MainForm? _form;
    private bool _exiting;

    public AppSettings Settings { get; }
    public List<Preset> Presets { get; private set; }

    public TrayContext(string[] args)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        Settings = Storage.LoadSettings();
        Presets = Storage.LoadAllPresets();
        _engine = new Customizer();

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => BuildMenu(menu);
        BuildMenu(menu);
        _tray = new NotifyIcon
        {
            Icon = AppIcon.Get(),
            Text = "CustomWin — кастомизация Windows",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };

        // Аргументы: --tray (тихий старт), --preset "Имя"
        int pi = Array.FindIndex(args, a => a.Equals("--preset", StringComparison.OrdinalIgnoreCase));
        var preset = pi >= 0 && pi + 1 < args.Length
            ? Presets.FirstOrDefault(p => p.Name.Equals(args[pi + 1], StringComparison.OrdinalIgnoreCase))
            : null;

        try
        {
            if (preset != null) ApplyPreset(preset);
            else _engine.Apply(Settings.Current, system: false); // восстановить фоновые эффекты после перезагрузки
        }
        catch (Exception ex) { ReportError(ex); }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => _ui.Post(_ => ShowMain(), null), null, -1, false);

        if (args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase))) TrimMemory();
        else ShowMain();
    }

    // ------------------------------------------------------------------ действия

    /// <returns>true, если желателен перезапуск Проводника.</returns>
    public bool ApplyProfile(Profile profile, string? presetName = null)
    {
        Settings.Current = Json.Clone(profile);
        Settings.LastPreset = presetName;
        bool needsExplorer = _engine.Apply(Settings.Current, system: true);
        Storage.SaveSettings(Settings);
        return needsExplorer;
    }

    public bool ApplyPreset(Preset preset) => ApplyProfile(preset.Profile, preset.Name);

    public void ReloadPresets() => Presets = Storage.LoadAllPresets();

    public async Task RestartExplorerAsync()
    {
        await Task.Run(SystemTweaks.RestartExplorer);
        // Новые окна Проводника получат оформление через события; панели задач нужна пауза
        await Task.Delay(2500);
        _engine.Apply(Settings.Current, system: false);
    }

    public void SetAutostart(bool enable)
    {
        SystemTweaks.SetAutostart(enable);
        Settings.Autostart = enable;
        Storage.SaveSettings(Settings);
    }

    private void ApplyRuntimeOnly()
    {
        _engine.Apply(Settings.Current, system: false);
        Storage.SaveSettings(Settings);
    }

    // ------------------------------------------------------------------ трей

    private void BuildMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();
        menu.Items.Add("Открыть настройки", null, (_, _) => ShowMain()).Font = new Font(menu.Font, FontStyle.Bold);

        var presets = new ToolStripMenuItem("Пресеты");
        foreach (var p in Presets)
        {
            var item = new ToolStripMenuItem(p.ToString(), null, (_, _) => TrayApply(p))
            {
                Checked = p.Name == Settings.LastPreset,
                ToolTipText = p.Description,
            };
            presets.DropDownItems.Add(item);
        }
        menu.Items.Add(presets);
        menu.Items.Add(new ToolStripSeparator());

        AddToggle(menu, "Оформление окон", Settings.Current.Windows.Enabled, v => Settings.Current.Windows.Enabled = v);
        AddToggle(menu, "Фон за окнами", Settings.Current.Backdrop.ApplyToWindows, v => Settings.Current.Backdrop.ApplyToWindows = v);
        AddToggle(menu, "Фон за меню", Settings.Current.Backdrop.ApplyToMenus, v => Settings.Current.Backdrop.ApplyToMenus = v);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Перезапустить Проводник", null, async (_, _) => await RestartExplorerAsync());
        menu.Items.Add("Выход", null, (_, _) => Exit());
    }

    private void AddToggle(ContextMenuStrip menu, string text, bool value, Action<bool> set)
    {
        var item = new ToolStripMenuItem(text) { Checked = value, CheckOnClick = true };
        item.CheckedChanged += (_, _) =>
        {
            set(item.Checked);
            ApplyRuntimeOnly();
            _form?.RefreshFromSettings();
        };
        menu.Items.Add(item);
    }

    private void TrayApply(Preset p)
    {
        try
        {
            bool explorer = ApplyPreset(p);
            _form?.RefreshFromSettings();
            _tray.ShowBalloonTip(3000, "CustomWin", explorer
                ? $"Пресет «{p.Name}» применён. Часть изменений появится после перезапуска Проводника (меню в трее)."
                : $"Пресет «{p.Name}» применён.", ToolTipIcon.Info);
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public void ShowMain()
    {
        if (_exiting) return;
        if (_form == null || _form.IsDisposed)
        {
            _form = new MainForm(this);
            _form.FormClosed += (_, _) => { _form = null; TrimMemory(); };
        }
        _form.Show();
        if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    public void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        _form?.Close();
        _showWait.Unregister(null);
        _showEvent.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _engine.Dispose(); // возвращаем окна в исходный вид
        try { Storage.SaveSettings(Settings); } catch { }
        ExitThread();
    }

    public void ReportError(Exception ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(Storage.DataDir, "error.log"), $"[{DateTime.Now:u}] {ex}\n\n");
            _tray.ShowBalloonTip(4000, "CustomWin: ошибка", ex.Message, ToolTipIcon.Warning);
        }
        catch { }
    }

    public void EmergencyRestore()
    {
        try { _engine.Dispose(); } catch { }
    }

    private static void TrimMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var self = Process.GetCurrentProcess();
        Win32.EmptyWorkingSet(self.Handle);
    }
}
