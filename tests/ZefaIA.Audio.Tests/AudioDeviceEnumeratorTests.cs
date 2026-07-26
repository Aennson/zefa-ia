using Xunit;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class AudioDeviceEnumeratorTests
{
    [RequiresAudioDeviceFact]
    public void GetMicrophones_ReturnsDeviceList()
    {
        var devices = AudioDeviceEnumerator.GetMicrophones();
        Assert.NotNull(devices);
    }

    [RequiresAudioDeviceFact]
    public void GetOutputDevices_ReturnsDeviceList()
    {
        var devices = AudioDeviceEnumerator.GetOutputDevices();
        Assert.NotNull(devices);
    }
}
