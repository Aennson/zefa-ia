using NAudio.CoreAudioApi;

namespace ZefaIA.Audio;

public static class AudioDeviceEnumerator
{
    public static IReadOnlyList<AudioDeviceInfo> GetMicrophones()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator
            .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName, AudioDeviceType.Microphone))
            .ToList();
    }

    public static IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName, AudioDeviceType.Output))
            .ToList();
    }
}

public record AudioDeviceInfo(string Id, string Name, AudioDeviceType Type);

public enum AudioDeviceType
{
    Microphone,
    Output
}
