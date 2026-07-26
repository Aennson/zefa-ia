using Xunit;
using ZefaIA.Core.Resilience;

namespace ZefaIA.App.Tests;

public class SensitiveDataScrubberTests
{
    #region API keys

    [Fact]
    public void Scrub_AnthropicKey_IsRedacted()
    {
        var input = "Request failed with key sk-ant-api03-AbCdEf123456_XyZ-789 rejected";

        var result = SensitiveDataScrubber.Scrub(input);

        Assert.DoesNotContain("sk-ant-api03", result);
        Assert.Contains(SensitiveDataScrubber.Redacted, result);
    }

    [Fact]
    public void Scrub_AnthropicKey_CaseInsensitive()
    {
        var result = SensitiveDataScrubber.Scrub("SK-ANT-API03-ABCDEFGH12345678");

        Assert.DoesNotContain("ABCDEFGH", result);
    }

    [Fact]
    public void Scrub_ElevenLabsKey_IsRedacted()
    {
        var input = "auth failed: sk_a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";

        var result = SensitiveDataScrubber.Scrub(input);

        Assert.DoesNotContain("a1b2c3d4e5f6", result);
        Assert.Contains(SensitiveDataScrubber.Redacted, result);
    }

    [Fact]
    public void Scrub_BearerToken_IsRedacted()
    {
        var input = "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";

        var result = SensitiveDataScrubber.Scrub(input);

        Assert.DoesNotContain("eyJhbGci", result);
        Assert.Contains("Bearer [REDACTED]", result);
    }

    [Theory]
    [InlineData("x-api-key: abcdef1234567890")]
    [InlineData("\"api_key\": \"abcdef1234567890\"")]
    [InlineData("ApiKey=abcdef1234567890")]
    [InlineData("api-key : abcdef1234567890")]
    public void Scrub_ApiKeyAssignments_AreRedacted(string input)
    {
        var result = SensitiveDataScrubber.Scrub(input);

        Assert.DoesNotContain("abcdef1234567890", result);
    }

    [Fact]
    public void Scrub_MultipleSecretsInOneString_AllRedacted()
    {
        var input = "key1=sk-ant-api03-AAAAAAAAAAAA and key2=sk_bbbbbbbbbbbbbbbbbbbb";

        var result = SensitiveDataScrubber.Scrub(input);

        Assert.DoesNotContain("sk-ant-api03-AAAA", result);
        Assert.DoesNotContain("sk_bbbb", result);
    }

    #endregion

    #region User paths

    [Fact]
    public void ScrubUserPaths_WindowsUserDirectory_IsAnonymized()
    {
        var input = @"Could not open C:\Users\joaosilva\AppData\Roaming\ZefaIA\meetings.db";

        var result = SensitiveDataScrubber.ScrubUserPaths(input);

        Assert.DoesNotContain("joaosilva", result);
        Assert.Contains(@"%USER%", result);
        // The rest of the path stays readable for debugging.
        Assert.Contains("ZefaIA", result);
        Assert.Contains("meetings.db", result);
    }

    [Fact]
    public void ScrubUserPaths_PreservesDriveLetter()
    {
        var result = SensitiveDataScrubber.ScrubUserPaths(@"D:\Users\ana\file.txt");

        Assert.StartsWith(@"D:\Users\", result);
    }

    [Fact]
    public void ScrubUserPaths_NoUserPath_LeavesInputAlone()
    {
        const string input = @"C:\Program Files\ZefaIA\app.exe";

        Assert.Equal(input, SensitiveDataScrubber.ScrubUserPaths(input));
    }

    #endregion

    #region Safe input

    [Fact]
    public void Scrub_Null_ReturnsEmpty()
    {
        Assert.Equal("", SensitiveDataScrubber.Scrub(null));
    }

    [Fact]
    public void Scrub_Empty_ReturnsEmpty()
    {
        Assert.Equal("", SensitiveDataScrubber.Scrub(""));
    }

    [Fact]
    public void Scrub_OrdinaryMessage_IsUnchanged()
    {
        const string input = "Audio device disconnected, retrying in 2s";

        Assert.Equal(input, SensitiveDataScrubber.Scrub(input));
    }

    [Fact]
    public void Scrub_ShortTokenLikeString_NotOverRedacted()
    {
        // "sk_short" is below the minimum key length; redacting it would make
        // ordinary log lines unreadable.
        const string input = "value sk_short here";

        Assert.Equal(input, SensitiveDataScrubber.Scrub(input));
    }

    #endregion

    #region Exceptions

    [Fact]
    public void ScrubException_Null_ReturnsEmpty()
    {
        Assert.Equal("", SensitiveDataScrubber.ScrubException(null));
    }

    [Fact]
    public void ScrubException_IncludesTypeAndMessage()
    {
        var ex = new InvalidOperationException("something broke");

        var result = SensitiveDataScrubber.ScrubException(ex);

        Assert.Contains("InvalidOperationException", result);
        Assert.Contains("something broke", result);
    }

    [Fact]
    public void ScrubException_RedactsSecretInMessage()
    {
        var ex = new InvalidOperationException("bad key sk-ant-api03-SECRETVALUE123");

        var result = SensitiveDataScrubber.ScrubException(ex);

        Assert.DoesNotContain("SECRETVALUE123", result);
    }

    [Fact]
    public void ScrubException_IncludesInnerException()
    {
        var inner = new ArgumentException("inner detail");
        var outer = new InvalidOperationException("outer", inner);

        var result = SensitiveDataScrubber.ScrubException(outer);

        Assert.Contains("outer", result);
        Assert.Contains("inner detail", result);
        Assert.Contains("Inner", result);
    }

    [Fact]
    public void ScrubException_RedactsSecretInInnerException()
    {
        var inner = new ArgumentException("key sk-ant-api03-NESTEDSECRET99");
        var outer = new InvalidOperationException("wrapper", inner);

        var result = SensitiveDataScrubber.ScrubException(outer);

        Assert.DoesNotContain("NESTEDSECRET99", result);
    }

    #endregion
}

public class CrashReporterTests
{
    [Fact]
    public void BuildReport_ContainsEnvironmentContext()
    {
        var report = CrashReporter.BuildReport(new InvalidOperationException("boom"), "UI");

        Assert.Contains("Zefa IA", report);
        Assert.Contains("Origem: UI", report);
        Assert.Contains("boom", report);
        Assert.Contains(".NET:", report);
    }

    [Fact]
    public void BuildReport_StatesThatNothingIsTransmitted()
    {
        var report = CrashReporter.BuildReport(new Exception("x"), "Task");

        Assert.Contains("nao e enviado", report);
    }

    [Fact]
    public void BuildReport_RedactsSecrets()
    {
        var ex = new InvalidOperationException("failed with sk-ant-api03-LEAKEDKEY12345");

        var report = CrashReporter.BuildReport(ex, "UI");

        Assert.DoesNotContain("LEAKEDKEY12345", report);
        Assert.Contains(SensitiveDataScrubber.Redacted, report);
    }

    [Fact]
    public void Report_WritesFileToGivenDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"zefa_crash_{Guid.NewGuid():N}");
        try
        {
            var reporter = new CrashReporter(dir);

            var path = reporter.Report(new InvalidOperationException("test crash"), "UI");

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            Assert.Contains("test crash", File.ReadAllText(path!));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Report_KeepsOnlyMostRecentReports()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"zefa_crash_{Guid.NewGuid():N}");
        try
        {
            var reporter = new CrashReporter(dir) { MaxReportsKept = 3 };

            // Distinct source names keep the filenames unique within one second.
            for (var i = 0; i < 6; i++)
                reporter.Report(new Exception($"crash {i}"), $"src{i}");

            var files = Directory.GetFiles(dir, "crash_*.txt");
            Assert.True(files.Length <= 3, $"expected at most 3 reports, found {files.Length}");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
