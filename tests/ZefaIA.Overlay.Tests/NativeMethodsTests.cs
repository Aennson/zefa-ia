using Xunit;

namespace ZefaIA.Overlay.Tests;

public class NativeMethodsTests
{
    [Fact]
    public void WindowStyle_Constants_HaveCorrectValues()
    {
        Assert.Equal(0x00000020, NativeMethods.WS_EX_TRANSPARENT);
        Assert.Equal(0x00080000, NativeMethods.WS_EX_LAYERED);
        Assert.Equal(0x00000080, NativeMethods.WS_EX_TOOLWINDOW);
        Assert.Equal(0x08000000, NativeMethods.WS_EX_NOACTIVATE);
    }

    [Fact]
    public void DisplayAffinity_Constants_HaveCorrectValues()
    {
        Assert.Equal(0x00000000u, NativeMethods.WDA_NONE);
        Assert.Equal(0x00000001u, NativeMethods.WDA_MONITOR);
        Assert.Equal(0x00000011u, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    [Fact]
    public void HitTest_Constants_HaveCorrectValues()
    {
        Assert.Equal(0x0084, NativeMethods.WM_NCHITTEST);
        Assert.Equal(-1, NativeMethods.HTTRANSPARENT);
        Assert.Equal(1, NativeMethods.HTCLIENT);
        Assert.Equal(2, NativeMethods.HTCAPTION);
    }

    [Fact]
    public void GWL_EXSTYLE_IsCorrect()
    {
        Assert.Equal(-20, NativeMethods.GWL_EXSTYLE);
    }
}
