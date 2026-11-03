using System.Text.Json;

namespace CustomWin.Core;

public sealed class AppSettings
{
    public Profile Current { get; set; } = new();
    public string? LastPreset { get; set; }
    public bool Autostart { get; set; }
}

internal static class Storage
{
    /// <summary>Папка данных. Если рядом с exe лежит файл portable.txt — храним всё рядом с exe.</summary>
    public static string DataDir { get; } = ResolveDataDir();
    public static string PresetsDir => Path.Combine(DataDir, "presets");
    public static string GeneratedDir => Path.Combine(DataDir, "generated");
    private static string SettingsPath => Path.Combine(DataDir, "settings.json");

    private static string ResolveDataDir()
    {
        var exeDir = AppContext.BaseDirectory;
        var dir = File.Exists(Path.Combine(exeDir, "portable.txt"))
            ? Path.Combine(exeDir, "data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomWin");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Json.Options) ?? new();
        }
        catch { /* битый файл — начинаем с чистого листа */ }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json.Options));
        File.Move(tmp, SettingsPath, overwrite: true);
    }

    // ---------------- Пресеты ----------------

    public static List<Preset> LoadAllPresets()
    {
        var list = BuiltInPresets.All();
        try
        {
            Directory.CreateDirectory(PresetsDir);
            foreach (var file in Directory.EnumerateFiles(PresetsDir, "*.json").OrderBy(f => f))
            {
                var p = TryReadPreset(file);
                if (p == null) continue;
                p.FilePath = file;
                list.Add(p);
            }
        }
        catch { }
        return list;
    }

    public static Preset? TryReadPreset(string file)
    {
        try { return JsonSerializer.Deserialize<Preset>(File.ReadAllText(file), Json.Options); }
        catch { return null; }
    }

    public static string SaveUserPreset(Preset preset)
    {
        Directory.CreateDirectory(PresetsDir);
        var safe = string.Concat(preset.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        if (safe.Length == 0) safe = "preset";
        var path = preset.FilePath ?? Path.Combine(PresetsDir, safe + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, Json.Options));
        preset.FilePath = path;
        return path;
    }

    public static void ExportPreset(Preset preset, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(preset, Json.Options));
}
