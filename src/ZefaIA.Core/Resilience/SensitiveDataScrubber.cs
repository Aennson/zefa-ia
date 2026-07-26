using System.Text.RegularExpressions;

namespace ZefaIA.Core.Resilience;

/// <summary>
/// Redacts secrets and personal data before anything is written to a log or crash
/// file. Two categories matter here: API keys (a leaked key is billable) and the
/// user's Windows account name, which appears in every %APPDATA% path and is
/// personal data under LGPD.
///
/// Meeting transcription text is never passed to the logger in the first place —
/// this is the backstop for exception messages that may quote it.
/// </summary>
public static partial class SensitiveDataScrubber
{
    public const string Redacted = "[REDACTED]";

    public static string Scrub(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? "";

        var result = AnthropicKeyPattern().Replace(input, Redacted);
        result = ElevenLabsKeyPattern().Replace(result, Redacted);
        result = BearerTokenPattern().Replace(result, $"Bearer {Redacted}");
        result = ApiKeyAssignmentPattern().Replace(result, $"$1{Redacted}");
        result = ScrubUserPaths(result);

        return result;
    }

    /// <summary>
    /// Replaces the user's home directory with %USERPROFILE% so paths stay readable
    /// for debugging without naming the account.
    /// </summary>
    public static string ScrubUserPaths(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
            input = input.Replace(home, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);

        // Catches paths produced on another machine or embedded in stack traces.
        return WindowsUserPathPattern().Replace(input, @"$1\%USER%\");
    }

    public static string ScrubException(Exception? ex)
    {
        if (ex == null) return "";

        var text = $"{ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";

        if (ex.InnerException != null)
            text += $"{Environment.NewLine}--- Inner ---{Environment.NewLine}{ScrubException(ex.InnerException)}";

        return Scrub(text);
    }

    [GeneratedRegex(@"sk-ant-[A-Za-z0-9_\-]{8,}", RegexOptions.IgnoreCase)]
    private static partial Regex AnthropicKeyPattern();

    [GeneratedRegex(@"\bsk_[A-Za-z0-9]{16,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex ElevenLabsKeyPattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9_\-\.]{8,}", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenPattern();

    /// <summary>Matches "api_key": "value", x-api-key=value, ApiKey: value, etc.</summary>
    [GeneratedRegex(@"((?:x-)?api[_\-]?key""?\s*[:=]\s*""?)[A-Za-z0-9_\-]{8,}", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyAssignmentPattern();

    [GeneratedRegex(@"([A-Za-z]:\\Users)\\[^\\/:*?""<>|\r\n]+\\", RegexOptions.IgnoreCase)]
    private static partial Regex WindowsUserPathPattern();
}
