using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZefaIA.Overlay;

/// <summary>Where a configured API key came from.</summary>
public enum ApiKeySource
{
    None,
    Settings,
    Environment
}

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

    // API keys.
    //
    // Stored DPAPI-encrypted (see SecretProtector), never as plaintext. The properties
    // that go into the JSON hold ciphertext; use the [JsonIgnore] pair below to read or
    // write the actual secret.
    public string AnthropicApiKeyProtected { get; set; } = "";
    public string ElevenLabsApiKeyProtected { get; set; } = "";

    [JsonIgnore]
    public string AnthropicApiKey
    {
        get => SecretProtector.Unprotect(AnthropicApiKeyProtected);
        set => AnthropicApiKeyProtected = SecretProtector.Protect(value);
    }

    [JsonIgnore]
    public string ElevenLabsApiKey
    {
        get => SecretProtector.Unprotect(ElevenLabsApiKeyProtected);
        set => ElevenLabsApiKeyProtected = SecretProtector.Protect(value);
    }

    /// <summary>
    /// The key the app should actually use: what the user typed in Settings, falling back
    /// to the environment variable.
    ///
    /// Settings wins deliberately. Someone who just pasted a key into the UI expects it to
    /// take effect, and would have no way to discover that a stale environment variable set
    /// months ago was silently overriding it.
    /// </summary>
    public string? ResolveAnthropicApiKey() =>
        Resolve(AnthropicApiKey, "ANTHROPIC_API_KEY");

    public string? ResolveElevenLabsApiKey() =>
        Resolve(ElevenLabsApiKey, "ELEVENLABS_API_KEY");

    private static string? Resolve(string stored, string environmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(stored))
            return stored;

        var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment;
    }

    /// <summary>Where a key is coming from, so the UI can say so instead of showing a blank box.</summary>
    public ApiKeySource AnthropicApiKeySource => SourceOf(AnthropicApiKey, "ANTHROPIC_API_KEY");
    public ApiKeySource ElevenLabsApiKeySource => SourceOf(ElevenLabsApiKey, "ELEVENLABS_API_KEY");

    private static ApiKeySource SourceOf(string stored, string environmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(stored))
            return ApiKeySource.Settings;

        return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentVariable))
            ? ApiKeySource.None
            : ApiKeySource.Environment;
    }

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
