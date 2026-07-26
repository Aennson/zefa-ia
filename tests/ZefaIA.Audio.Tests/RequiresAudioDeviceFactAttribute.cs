using NAudio.CoreAudioApi;
using Xunit;

namespace ZefaIA.Audio.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that runs only when the machine actually has the
/// audio endpoint the test needs. xUnit v2 requires <c>Skip</c> to be a compile-time
/// constant on the attribute, so the probe happens here in the constructor: on a
/// developer box with a mic and speakers these tests execute for real, and on CI (or a
/// headless VM) they report as skipped instead of failing for the wrong reason.
/// </summary>
public sealed class RequiresAudioDeviceFactAttribute : FactAttribute
{
    public RequiresAudioDeviceFactAttribute(AudioEndpoint endpoint = AudioEndpoint.Any)
    {
        var reason = Probe(endpoint);
        if (reason != null)
            Skip = reason;
    }

    private static string? Probe(AudioEndpoint endpoint)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();

            if (endpoint is AudioEndpoint.Capture or AudioEndpoint.Any &&
                enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Count == 0)
            {
                return "No active audio capture device on this machine";
            }

            if (endpoint is AudioEndpoint.Render or AudioEndpoint.Any &&
                enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Count == 0)
            {
                return "No active audio render device on this machine";
            }

            return null;
        }
        catch (Exception ex)
        {
            // No WASAPI at all (container, Server Core without the audio role).
            return $"Audio subsystem unavailable: {ex.Message}";
        }
    }
}

public enum AudioEndpoint
{
    Any,
    Capture,
    Render
}
