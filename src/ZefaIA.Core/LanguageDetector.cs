using ZefaIA.Core.Models;

namespace ZefaIA.Core;

public sealed class LanguageDetector
{
    private readonly Dictionary<string, int> _languageCounts = new();
    private readonly int _minSamples;
    private string? _override;
    private string _detectedLanguage = "auto";
    private bool _locked;

    public string DetectedLanguage => _override ?? _detectedLanguage;
    public bool IsDetected => _locked || _override != null;

    public event Action<string>? OnLanguageDetected;

    public LanguageDetector(int minSamples = 5)
    {
        _minSamples = minSamples;
    }

    public void SetOverride(string? language)
    {
        _override = language is "auto" or "" ? null : language;
        if (_override != null)
            OnLanguageDetected?.Invoke(_override);
    }

    public void ProcessSegment(TranscriptionSegment segment)
    {
        if (_locked || _override != null) return;
        if (string.IsNullOrEmpty(segment.Language) || segment.Language == "unknown") return;

        var lang = NormalizeLanguage(segment.Language);
        _languageCounts.TryGetValue(lang, out var count);
        _languageCounts[lang] = count + 1;

        var totalSamples = _languageCounts.Values.Sum();
        if (totalSamples >= _minSamples)
        {
            _detectedLanguage = GetTopLanguage();
            _locked = true;
            OnLanguageDetected?.Invoke(_detectedLanguage);
        }
    }

    public SpeakerLabels GetSpeakerLabels()
    {
        return DetectedLanguage switch
        {
            "en" => new SpeakerLabels("Me", "Other"),
            "es" => new SpeakerLabels("Yo", "Interlocutor"),
            "fr" => new SpeakerLabels("Moi", "Interlocuteur"),
            _ => new SpeakerLabels("Eu", "Interlocutor")
        };
    }

    public void Reset()
    {
        _languageCounts.Clear();
        _detectedLanguage = "auto";
        _locked = false;
    }

    internal string GetTopLanguage()
    {
        if (_languageCounts.Count == 0) return "auto";
        return _languageCounts.MaxBy(kv => kv.Value).Key;
    }

    internal static string NormalizeLanguage(string lang)
    {
        var lower = lang.ToLowerInvariant();
        if (lower.StartsWith("pt")) return "pt";
        if (lower.StartsWith("en")) return "en";
        if (lower.StartsWith("es")) return "es";
        if (lower.StartsWith("fr")) return "fr";
        if (lower.StartsWith("de")) return "de";
        if (lower.StartsWith("it")) return "it";
        if (lower.StartsWith("ja")) return "ja";
        if (lower.StartsWith("zh")) return "zh";
        return lower.Length > 2 ? lower[..2] : lower;
    }
}

public record SpeakerLabels(string Self, string Other);
