using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisplayBlackout.Services;

internal sealed class SettingsService
{
    private static readonly string s_settingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DisplayBlackout");

    private static readonly string s_settingsPath = Path.Combine(s_settingsDir, "settings.json");

    private AppSettings _settings;

    public SettingsService()
    {
        _settings = Load();
    }

    /// <summary>
    /// Creates a stable identifier for a monitor based on its bounds.
    /// Format: "X,Y,W,H" (e.g., "0,0,1920,1080")
    /// </summary>
    public static string GetMonitorKey(NativeMethods.RECT bounds)
        => $"{bounds.Left},{bounds.Top},{bounds.Width},{bounds.Height}";

    public HashSet<string>? LoadSelectedMonitorBounds()
    {
        if (_settings.SelectedMonitorBounds is not { Length: > 0 } str)
        {
            return null;
        }

        var bounds = new HashSet<string>();
        foreach (var part in str.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            bounds.Add(part);
        }
        return bounds.Count > 0 ? bounds : null;
    }

    public void SaveSelectedMonitorBounds(HashSet<string>? monitorBounds)
    {
        _settings.SelectedMonitorBounds = monitorBounds is { Count: > 0 }
            ? string.Join('|', monitorBounds)
            : null;
        Save();
    }

    public int LoadOpacity() => Math.Clamp(_settings.Opacity, 0, 100);

    public void SaveOpacity(int opacity)
    {
        _settings.Opacity = opacity;
        Save();
    }

    public bool LoadClickThrough() => _settings.ClickThrough;

    public void SaveClickThrough(bool clickThrough)
    {
        _settings.ClickThrough = clickThrough;
        Save();
    }

    public string LoadTheme() => _settings.Theme ?? "System";

    public void SaveTheme(string theme)
    {
        _settings.Theme = theme;
        Save();
    }

    public string? LoadAccent() => _settings.Accent;

    public void SaveAccent(string accent)
    {
        _settings.Accent = accent;
        Save();
    }

    public void ResetAll()
    {
        _settings = new AppSettings();
        Save();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(s_settingsPath))
            {
                var json = File.ReadAllText(s_settingsPath);
                return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupted settings file — use defaults
        }
        return new AppSettings();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(s_settingsDir);
            var json = JsonSerializer.Serialize(_settings, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(s_settingsPath, json);
        }
        catch
        {
            // Best-effort save
        }
    }
}

internal sealed class AppSettings
{
    public string? SelectedMonitorBounds { get; set; }
    public int Opacity { get; set; } = 100;
    public bool ClickThrough { get; set; }
    public string? Theme { get; set; }
    public string? Accent { get; set; }
}

[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
