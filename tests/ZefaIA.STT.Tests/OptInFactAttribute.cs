using Xunit;

namespace ZefaIA.STT.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that need something the build machine cannot
/// be assumed to have: a large model download, network access, or a paid API key.
/// They stay off by default and run when the named environment variable is set to
/// <c>1</c>/<c>true</c>, so they are executable on demand rather than dead code behind a
/// hard-coded <c>Skip</c>.
/// </summary>
public sealed class OptInFactAttribute : FactAttribute
{
    public OptInFactAttribute(string environmentVariable, string requirement)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        var enabled = value is "1" ||
                      string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        if (!enabled)
            Skip = $"{requirement}. Set {environmentVariable}=1 to run.";
    }
}
