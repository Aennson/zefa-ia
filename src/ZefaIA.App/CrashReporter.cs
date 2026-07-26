using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZefaIA.Core.Resilience;

namespace ZefaIA.App;

/// <summary>
/// Writes crash details to a local file. Nothing is transmitted anywhere — the
/// report exists so the user can read or attach it themselves, and everything in
/// it goes through the scrubber first.
/// </summary>
public sealed class CrashReporter
{
    private readonly string _crashDirectory;
    private readonly ILogger<CrashReporter> _logger;

    public int MaxReportsKept { get; init; } = 10;

    public static string DefaultCrashDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ZefaIA", "crashes");

    public CrashReporter(string? crashDirectory = null, ILogger<CrashReporter>? logger = null)
    {
        _crashDirectory = crashDirectory ?? DefaultCrashDirectory;
        _logger = logger ?? NullLogger<CrashReporter>.Instance;
    }

    public string? Report(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(_crashDirectory);

            var path = Path.Combine(
                _crashDirectory,
                $"crash_{DateTime.Now:yyyyMMdd_HHmmss}_{source}.txt");

            File.WriteAllText(path, BuildReport(exception, source), Encoding.UTF8);
            PruneOldReports();

            _logger.LogError("Crash report written to {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            // A failure to write the crash file must never mask the original crash.
            _logger.LogError(ex, "Could not write crash report");
            return null;
        }
    }

    internal static string BuildReport(Exception exception, string source)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Zefa IA - Relatorio de Falha");
        sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Origem: {source}");
        sb.AppendLine($"Versao do SO: {Environment.OSVersion}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Arquitetura: {(Environment.Is64BitProcess ? "x64" : "x86")}");
        sb.AppendLine();
        sb.AppendLine("--- Excecao ---");
        sb.AppendLine(SensitiveDataScrubber.ScrubException(exception));
        sb.AppendLine();
        sb.AppendLine("Este arquivo fica somente na sua maquina e nao e enviado a lugar nenhum.");

        return sb.ToString();
    }

    /// <summary>Keeps the newest reports so the folder cannot grow without bound.</summary>
    private void PruneOldReports()
    {
        var files = new DirectoryInfo(_crashDirectory)
            .GetFiles("crash_*.txt")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(MaxReportsKept);

        foreach (var file in files)
        {
            try { file.Delete(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete old crash report {File}", file.Name); }
        }
    }
}
