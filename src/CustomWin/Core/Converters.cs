using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomWin.Core;

/// <summary>Показывает значения enum в PropertyGrid по-русски (из [Description]).</summary>
internal sealed class RuEnumConverter : EnumConverter
{
    public RuEnumConverter(Type type) : base(type) { }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value != null && value.GetType().IsEnum)
        {
            var field = value.GetType().GetField(value.ToString()!);
            var description = field?.GetCustomAttribute<DescriptionAttribute>();
            if (description != null) return description.Description;
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            foreach (var field in EnumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var description = field.GetCustomAttribute<DescriptionAttribute>();
                if (description?.Description == text || field.Name == text)
                    return field.GetValue(null)!;
            }
        }
        return base.ConvertFrom(context, culture, value);
    }
}

/// <summary>Да / Нет вместо True / False.</summary>
internal sealed class RuBoolConverter : BooleanConverter
{
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is bool b) return b ? "Да" : "Нет";
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            if (s == "Да") return true;
            if (s == "Нет") return false;
        }
        return base.ConvertFrom(context, culture, value);
    }
}

/// <summary>Цвет в JSON как "#RRGGBB" (или null = «не менять»).</summary>
internal sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return Color.Empty;
        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s)) return Color.Empty;
        try { return ColorTranslator.FromHtml(s); } catch { return Color.Empty; }
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        if (value.IsEmpty) { writer.WriteNullValue(); return; }
        writer.WriteStringValue($"#{value.R:X2}{value.G:X2}{value.B:X2}");
    }
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(), new ColorJsonConverter() },
    };

    public static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
}

internal static class OsInfo
{
    public static int Build { get; } = Environment.OSVersion.Version.Build;
    public static bool IsWin11 => Build >= 22000;
    /// <summary>Mica / Acrylic через DWMWA_SYSTEMBACKDROP_TYPE (Windows 11 22H2+).</summary>
    public static bool SupportsBackdrop => Build >= 22621;
}
