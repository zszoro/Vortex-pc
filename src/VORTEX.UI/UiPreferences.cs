using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace VORTEX.UI;

public sealed class UiPreferences
{
    public string Theme { get; set; } = "Black";
    public string PetAppearance { get; set; } = "Vortex";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VORTEX", "ui-preferences.json");

    public static UiPreferences Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void ApplyTheme(string theme)
    {
        var palette = theme switch
        {
            "Nebula" => ("#000000", "#050505", "#F472D0", "#B7A4CA"),
            "Oceano" => ("#000000", "#050505", "#22D3EE", "#91B8C3"),
            "Claro" => ("#000000", "#050505", "#FFFFFF", "#C8CED8"),
            "Vortex" => ("#030303", "#0D0D0D", "#F2F2F2", "#9A9A9A"),
            _ => ("#000000", "#000000", "#FFFFFF", "#A0A0A0")
        };
        Application.Current.Resources["VortexBackgroundBrush"] = Brush(palette.Item1);
        Application.Current.Resources["SurfaceBrush"] = Brush(palette.Item2);
        Application.Current.Resources["AccentBrush"] = Brush(palette.Item3);
        Application.Current.Resources["MutedTextBrush"] = Brush(palette.Item4);
    }

    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));
}
