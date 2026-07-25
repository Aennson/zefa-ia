using Xunit;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class AudioDeviceEnumeratorTests
{
    [Fact(Skip = "Requires Windows audio devices")]
    public void GetMicrophones_ReturnsDeviceList()
    {
        var devices = AudioDeviceEnumerator.GetMicrophones();
        Assert.NotNull(devices);
    }

    [Fact(Skip = "Requires Windows audio devices")]
    public void GetOutputDevices_ReturnsDeviceList()
    {
        var devices = AudioDeviceEnumerator.GetOutputDevices();
        Assert.NotNull(devices);
    }
}
