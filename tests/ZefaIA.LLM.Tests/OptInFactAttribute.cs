using Xunit;

namespace ZefaIA.LLM.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that need a credential or network access the
/// build machine cannot be assumed to have. Off by default, enabled by setting the named
/// environment variable to <c>1</c>/<c>true</c>.
///
/// Deliberately duplicated from ZefaIA.STT.Tests rather than shared: it is twenty lines,
/// and a shared test-support project would couple the two suites for no real gain.
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
