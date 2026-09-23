using System.IO;
using System.Text.Json;
using FloatDock.Core;

namespace FloatDock.Windows;

public sealed class DockSettings
{
    public bool ShowPlate { get; set; } = true;
    public bool ReplaceTaskbar { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public bool ShowMedia { get; set; } = true;
    public bool OnlineLyrics { get; set; }
    public int IconSize { get; set; } = 40;
    public List<AppPin> Pins { get; set; } = [new("文件资源管理器", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))];
}

public static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloatDock");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public static DockSettings Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(FilePath)) return new();
            var settings = JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(FilePath)) ?? throw new JsonException("Empty settings");
            settings.IconSize = Math.Clamp(settings.IconSize, 32, 64);
            settings.Pins = (settings.Pins ?? []).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Path) && Path.IsPathFullyQualified(p.Path)
                && string.Equals(Path.GetExtension(p.Path), ".exe", StringComparison.OrdinalIgnoreCase))
                .DistinctBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToList();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            warning = "无法读取配置，将使用默认设置。原配置暂时保留；更改设置后会保存新配置。\n" + ex.Message;
            return new();
        }
    }

    public static void Save(DockSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
