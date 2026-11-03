using System.ComponentModel;
using System.Drawing.Design;

namespace CustomWin.Core;

// ============================ Перечисления ============================

[TypeConverter(typeof(RuEnumConverter))]
public enum Toggle
{
    [Description("Не менять")] Keep,
    [Description("Включить")] On,
    [Description("Выключить")] Off,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum ThemeMode
{
    [Description("Не менять")] Keep,
    [Description("Светлая")] Light,
    [Description("Тёмная")] Dark,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum TitleBarTheme
{
    [Description("Как в приложении")] Default,
    [Description("Тёмный")] Dark,
    [Description("Светлый")] Light,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum BackdropKind
{
    [Description("Как в приложении")] Default = 0,
    [Description("Без эффекта")] None = 1,
    [Description("Mica")] Mica = 2,
    [Description("Акрил (размытие)")] Acrylic = 3,
    [Description("Mica Alt (вкладки)")] MicaAlt = 4,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum CornerKind
{
    [Description("Как в системе")] Default = 0,
    [Description("Квадратные")] Square = 1,
    [Description("Скруглённые")] Round = 2,
    [Description("Слегка скруглённые")] RoundSmall = 3,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum WallpaperMode
{
    [Description("Не менять")] Keep,
    [Description("Картинка")] Image,
    [Description("Градиент")] Gradient,
    [Description("Слайд-шоу из папки")] Slideshow,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum WallpaperFit
{
    [Description("Заполнение")] Fill,
    [Description("По размеру")] Fit,
    [Description("Растянуть")] Stretch,
    [Description("Замостить")] Tile,
    [Description("По центру")] Center,
    [Description("Расширение на все мониторы")] Span,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum BackdropSource
{
    [Description("Обои рабочего стола")] Desktop,
    [Description("Своя картинка")] Image,
    [Description("Градиент")] Gradient,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum BackdropPlacement
{
    [Description("Закреплена на экране (окно как «окошко» в картинку)")] Screen,
    [Description("Растянута под каждое окно")] Window,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum TaskbarAlign
{
    [Description("Не менять")] Keep,
    [Description("Слева")] Left,
    [Description("По центру")] Center,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum SearchBoxMode
{
    [Description("Не менять")] Keep = -1,
    [Description("Скрыть")] Hidden = 0,
    [Description("Только значок")] Icon = 1,
    [Description("Поле поиска")] Box = 2,
}

[TypeConverter(typeof(RuEnumConverter))]
public enum ExplorerStart
{
    [Description("Не менять")] Keep,
    [Description("Этот компьютер")] ThisPC,
    [Description("Главная / Быстрый доступ")] Home,
}

// ============================ Разделы профиля ============================

public sealed class ThemeSettings
{
    [Category("1. Режим"), DisplayName("Тема приложений"),
     Description("Светлая или тёмная тема для приложений Windows (Параметры, Проводник, Блокнот и т.д.).")]
    public ThemeMode AppsTheme { get; set; } = ThemeMode.Keep;

    [Category("1. Режим"), DisplayName("Тема системы"),
     Description("Тема панели задач, меню «Пуск» и центра уведомлений.")]
    public ThemeMode SystemTheme { get; set; } = ThemeMode.Keep;

    [Category("1. Режим"), DisplayName("Эффекты прозрачности"),
     Description("Системная прозрачность (акрил) у «Пуска», панели задач и всплывающих окон. Через неё просвечивают ваши обои.")]
    public Toggle Transparency { get; set; } = Toggle.Keep;

    [Category("2. Акцентный цвет"), DisplayName("Цвет акцента"),
     Description("Акцентный цвет Windows. Пусто = не менять. Можно ввести #RRGGBB."),
     DefaultValue(typeof(Color), "")]
    public Color AccentColor { get; set; } = Color.Empty;

    [Category("2. Акцентный цвет"), DisplayName("Акцент на заголовках окон"),
     Description("Красить заголовки и рамки окон в цвет акцента.")]
    public Toggle AccentOnTitleBars { get; set; } = Toggle.Keep;

    [Category("2. Акцентный цвет"), DisplayName("Акцент на «Пуске» и панели задач"),
     Description("Работает только при тёмной теме системы.")]
    public Toggle AccentOnTaskbar { get; set; } = Toggle.Keep;
}

public sealed class WindowSettings
{
    [Category("1. Общее"), DisplayName("Включено"),
     Description("Применять оформление ко всем окнам приложений (в том числе к новым). Работает в фоне через системные события, без внедрения в чужие процессы."),
     TypeConverter(typeof(RuBoolConverter))]
    public bool Enabled { get; set; }

    [Category("1. Общее"), DisplayName("Исключения (процессы)"),
     Description("Имена .exe через запятую, к которым ничего не применять. Например: game.exe, obs64.exe")]
    public string ExcludedProcesses { get; set; } = "";

    [Category("2. Заголовок"), DisplayName("Тема заголовка"),
     Description("Принудительно тёмный или светлый заголовок окна.")]
    public TitleBarTheme TitleBar { get; set; } = TitleBarTheme.Default;

    [Category("2. Заголовок"), DisplayName("Цвет заголовка"),
     Description("Фон заголовка окна (Windows 11). Пусто = как в приложении."),
     DefaultValue(typeof(Color), "")]
    public Color CaptionColor { get; set; } = Color.Empty;

    [Category("2. Заголовок"), DisplayName("Цвет текста заголовка"),
     Description("Windows 11. Пусто = как в приложении."),
     DefaultValue(typeof(Color), "")]
    public Color CaptionTextColor { get; set; } = Color.Empty;

    [Category("3. Рамка"), DisplayName("Цвет рамки"),
     Description("Цвет рамки окна (Windows 11). Пусто = как в системе."),
     DefaultValue(typeof(Color), "")]
    public Color BorderColor { get; set; } = Color.Empty;

    [Category("3. Рамка"), DisplayName("Убрать рамку"),
     Description("Скрыть цветную рамку окна (Windows 11)."),
     TypeConverter(typeof(RuBoolConverter))]
    public bool HideBorder { get; set; }

    [Category("3. Рамка"), DisplayName("Углы окон"),
     Description("Форма углов окон (Windows 11).")]
    public CornerKind Corners { get; set; } = CornerKind.Default;

    [Category("4. Материал"), DisplayName("Фон окна (материал)"),
     Description("Mica / Акрил / Mica Alt — системные материалы Windows 11 22H2+, сквозь которые просвечивают обои. Видны там, где приложение само не закрашивает фон.")]
    public BackdropKind Backdrop { get; set; } = BackdropKind.Default;

    [Category("4. Материал"), DisplayName("Растянуть материал на всё окно (эксперимент)"),
     Description("Расширяет материал на клиентскую область. У части приложений это даёт эффект «стекла» на всё окно, у части может давать артефакты — добавьте такие в исключения."),
     TypeConverter(typeof(RuBoolConverter))]
    public bool ExtendIntoClientArea { get; set; }

    private int _opacity = 100;

    [Category("5. Прозрачность"), DisplayName("Непрозрачность всех окон, %"),
     Description("100 = обычные окна. 30–99 = окна полупрозрачные. Не применяется к окнам, которые уже используют собственную прозрачность.")]
    public int Opacity { get => _opacity; set => _opacity = Math.Clamp(value, 30, 100); }

    public bool HasDwmEffects() =>
        TitleBar != TitleBarTheme.Default || !CaptionColor.IsEmpty || !CaptionTextColor.IsEmpty ||
        !BorderColor.IsEmpty || HideBorder || Corners != CornerKind.Default ||
        Backdrop != BackdropKind.Default || ExtendIntoClientArea;

    public bool IsActive() => Enabled && (HasDwmEffects() || Opacity < 100);
}

public sealed class BackdropSettings
{
    [Category("1. Общее"), DisplayName("Фон за окнами"),
     Description("Показывать картинку позади активного окна, а само окно делать полупрозрачным. Получается «обои внутри окна»."),
     TypeConverter(typeof(RuBoolConverter))]
    public bool ApplyToWindows { get; set; }

    [Category("1. Общее"), DisplayName("Фон за меню"),
     Description("То же для классических меню (контекстные меню, меню «Файл/Правка»). В Windows 11 включите «Классическое контекстное меню», чтобы это работало и в Проводнике."),
     TypeConverter(typeof(RuBoolConverter))]
    public bool ApplyToMenus { get; set; }

    [Category("2. Картинка"), DisplayName("Источник"),
     Description("Откуда брать картинку для фона.")]
    public BackdropSource Source { get; set; } = BackdropSource.Desktop;

    [Category("2. Картинка"), DisplayName("Файл картинки"),
     Description("Используется, если источник = «Своя картинка». JPG, PNG, BMP."),
     Editor("System.Windows.Forms.Design.FileNameEditor, System.Windows.Forms.Design", typeof(UITypeEditor))]
    public string ImagePath { get; set; } = "";

    [Category("2. Картинка"), DisplayName("Градиент: цвет 1")]
    public Color GradientFrom { get; set; } = Color.FromArgb(0x1E, 0x1E, 0x3F);

    [Category("2. Картинка"), DisplayName("Градиент: цвет 2")]
    public Color GradientTo { get; set; } = Color.FromArgb(0x5B, 0x2A, 0x86);

    [Category("2. Картинка"), DisplayName("Расположение"),
     Description("«Закреплена на экране» — окно работает как окошко в общую картинку (красиво при перетаскивании). «Растянута» — картинка целиком в каждом окне.")]
    public BackdropPlacement Placement { get; set; } = BackdropPlacement.Screen;

    private int _blur, _dim = 15, _winOpacity = 90, _menuOpacity = 85;

    [Category("3. Эффекты"), DisplayName("Размытие (0–20)"),
     Description("Размытие картинки, чтобы текст в окнах читался лучше. Размытая картинка ещё и занимает меньше памяти.")]
    public int Blur { get => _blur; set => _blur = Math.Clamp(value, 0, 20); }

    [Category("3. Эффекты"), DisplayName("Затемнение, %"),
     Description("Затемнить картинку (0–90).")]
    public int Dim { get => _dim; set => _dim = Math.Clamp(value, 0, 90); }

    [Category("3. Эффекты"), DisplayName("Оттенок"),
     Description("Цветной оттенок поверх картинки. Пусто = без оттенка."), DefaultValue(typeof(Color), "")]
    public Color Tint { get; set; } = Color.Empty;

    [Category("4. Прозрачность"), DisplayName("Непрозрачность активного окна, %"),
     Description("Насколько сквозь окно видна картинка. 85–92 обычно лучший баланс читаемости и красоты.")]
    public int WindowOpacity { get => _winOpacity; set => _winOpacity = Math.Clamp(value, 40, 100); }

    [Category("4. Прозрачность"), DisplayName("Непрозрачность меню, %")]
    public int MenuOpacity { get => _menuOpacity; set => _menuOpacity = Math.Clamp(value, 40, 100); }

    public bool IsActive() => ApplyToWindows || ApplyToMenus;
}

public sealed class WallpaperSettings
{
    [Category("1. Обои"), DisplayName("Режим"),
     Description("Что поставить на рабочий стол.")]
    public WallpaperMode Mode { get; set; } = WallpaperMode.Keep;

    [Category("1. Обои"), DisplayName("Файл картинки"),
     Editor("System.Windows.Forms.Design.FileNameEditor, System.Windows.Forms.Design", typeof(UITypeEditor))]
    public string ImagePath { get; set; } = "";

    [Category("1. Обои"), DisplayName("Размещение")]
    public WallpaperFit Fit { get; set; } = WallpaperFit.Fill;

    [Category("2. Градиент"), DisplayName("Цвет 1")]
    public Color GradientFrom { get; set; } = Color.FromArgb(0x0F, 0x20, 0x27);

    [Category("2. Градиент"), DisplayName("Цвет 2")]
    public Color GradientTo { get; set; } = Color.FromArgb(0x2C, 0x53, 0x64);

    private int _angle = 135;

    [Category("2. Градиент"), DisplayName("Угол, °")]
    public int GradientAngle { get => _angle; set => _angle = ((value % 360) + 360) % 360; }

    [Category("3. Слайд-шоу"), DisplayName("Папка с картинками"),
     Editor("System.Windows.Forms.Design.FolderNameEditor, System.Windows.Forms.Design", typeof(UITypeEditor))]
    public string SlideshowFolder { get; set; } = "";

    private int _interval = 30;

    [Category("3. Слайд-шоу"), DisplayName("Интервал, минут")]
    public int SlideshowMinutes { get => _interval; set => _interval = Math.Clamp(value, 1, 1440); }

    [Category("3. Слайд-шоу"), DisplayName("Случайный порядок"), TypeConverter(typeof(RuBoolConverter))]
    public bool Shuffle { get; set; } = true;
}

public sealed class ShellSettings
{
    [Category("1. Панель задач"), DisplayName("Выравнивание значков"),
     Description("Windows 11.")]
    public TaskbarAlign TaskbarAlignment { get; set; } = TaskbarAlign.Keep;

    [Category("1. Панель задач"), DisplayName("Кнопка «Представление задач»")]
    public Toggle TaskViewButton { get; set; } = Toggle.Keep;

    [Category("1. Панель задач"), DisplayName("Поиск на панели задач")]
    public SearchBoxMode Search { get; set; } = SearchBoxMode.Keep;

    [Category("2. Проводник"), DisplayName("Классическое контекстное меню"),
     Description("Windows 11: сразу полное меню без «Показать дополнительные параметры». Нужен перезапуск Проводника.")]
    public Toggle ClassicContextMenu { get; set; } = Toggle.Keep;

    [Category("2. Проводник"), DisplayName("Показывать расширения файлов")]
    public Toggle ShowFileExtensions { get; set; } = Toggle.Keep;

    [Category("2. Проводник"), DisplayName("Показывать скрытые файлы")]
    public Toggle ShowHiddenFiles { get; set; } = Toggle.Keep;

    [Category("2. Проводник"), DisplayName("Открывать Проводник на")]
    public ExplorerStart OpenExplorerTo { get; set; } = ExplorerStart.Keep;

    [Category("3. Анимации и отклик"), DisplayName("Анимации окон и интерфейса"),
     Description("Выключение ускоряет интерфейс на слабых ПК.")]
    public Toggle Animations { get; set; } = Toggle.Keep;

    [Category("3. Анимации и отклик"), DisplayName("Анимация меню"),
     Description("Плавное появление меню. Выключите, если используете «Фон за меню» — так меню не мерцают.")]
    public Toggle MenuAnimation { get; set; } = Toggle.Keep;

    private int _menuDelay = -1;

    [Category("3. Анимации и отклик"), DisplayName("Задержка подменю, мс"),
     Description("-1 = не менять. Стандарт Windows: 400. Быстро: 0–100.")]
    public int MenuShowDelay { get => _menuDelay; set => _menuDelay = Math.Clamp(value, -1, 2000); }
}

/// <summary>Полный набор настроек оформления. Пресет = именованный профиль.</summary>
public sealed class Profile
{
    public ThemeSettings Theme { get; set; } = new();
    public WindowSettings Windows { get; set; } = new();
    public BackdropSettings Backdrop { get; set; } = new();
    public WallpaperSettings Wallpaper { get; set; } = new();
    public ShellSettings Shell { get; set; } = new();
}

public sealed class Preset
{
    public string Name { get; set; } = "Без названия";
    public string Description { get; set; } = "";
    public Profile Profile { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public bool BuiltIn { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? FilePath { get; set; }

    public override string ToString() => BuiltIn ? Name : Name + "  (мой)";
}
