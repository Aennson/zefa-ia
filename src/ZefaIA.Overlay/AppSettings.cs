using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZefaIA.Overlay;

public class AppSettings
{
    // STT
    public string SttProvider { get; set; } = "WhisperLocal";
    public string WhisperModelSize { get; set; } = "base";
    public string Language { get; set; } = "auto";
    public bool UseGPU { get; set; }

    // Profile
    public string UserName { get; set; } = "";
    public string UserRole { get; set; } = "";
    public string UserExpertise { get; set; } = "";
    public string PreferredTone { get; set; } = "Formal";
    public string AdditionalContext { get; set; } = "";

    // Hotkeys
    public string HotkeySuggestion { get; set; } = "Ctrl+Shift+Space";
    public string HotkeyToggle { get; set; } = "Ctrl+Shift+Z";
    public string HotkeyCopy { get; set; } = "Ctrl+Shift+C";

    // Overlay
    public double OverlayOpacity { get; set; } = 0.85;
    public double OverlayFontSize { get; set; } = 14;
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.BottomRight;
    public int AutoHideSeconds { get; set; } = 30;
    public bool ExcludeFromCapture { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static AppSettings FromJson(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();

    public async Task SaveAsync(string path)
    {
        var json = ToJson();
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<AppSettings> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return new AppSettings();

        var json = await File.ReadAllTextAsync(path);
        return FromJson(json);
    }
}
