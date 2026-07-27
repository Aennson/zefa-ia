using Xunit;
using ZefaIA.STT;

namespace ZefaIA.STT.Tests;

public class ModelPathResolutionTests
{
    [Fact]
    public void RelativePath_ResolvesNextToTheExecutable_NotTheWorkingDirectory()
    {
        var resolved = WhisperSTTProvider.ResolveModelDirectory("./models");

        Assert.True(Path.IsPathRooted(resolved));
        Assert.StartsWith(
            Path.GetFullPath(AppContext.BaseDirectory),
            resolved,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RelativePath_DoesNotFollowTheProcessWorkingDirectory()
    {
        // The bug this guards: launching the exe from another folder re-downloaded the
        // 141 MB model there instead of using the copy shipped with the app.
        var original = Directory.GetCurrentDirectory();
        var elsewhere = Path.Combine(Path.GetTempPath(), $"zefa_cwd_{Guid.NewGuid():N}");
        Directory.CreateDirectory(elsewhere);

        try
        {
            Directory.SetCurrentDirectory(elsewhere);
            var resolved = WhisperSTTProvider.ResolveModelDirectory("./models");

            Assert.DoesNotContain(
                Path.GetFileName(elsewhere), resolved, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            try { Directory.Delete(elsewhere, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void AbsolutePath_IsHonouredAsGiven()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "zefa_models_explicit");

        Assert.Equal(absolute, WhisperSTTProvider.ResolveModelDirectory(absolute));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MissingPath_FallsBackToTheDefaultDirectory(string? configured)
    {
        var resolved = WhisperSTTProvider.ResolveModelDirectory(configured!);

        Assert.True(Path.IsPathRooted(resolved));
        Assert.EndsWith("models", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeLoadFailure_NamesTheCauseItActuallyFound()
    {
        // The first version of this message always blamed a missing VC++ runtime. A real
        // user then went looking for a redistributable that was already installed, when
        // the actual fault was the native DLL being dropped by the single-file publish.
        var message = WhisperSTTProvider.DescribeNativeLoadFailure();
        var nativeExists = File.Exists(
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "whisper.dll"));

        Assert.Contains("runtimes", message);

        if (nativeExists)
            Assert.Contains("Visual C++", message);
        else
            Assert.Contains("empacotamento", message);
    }
}
