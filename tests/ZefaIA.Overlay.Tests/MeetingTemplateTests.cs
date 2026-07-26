using ZefaIA.Overlay;

namespace ZefaIA.Overlay.Tests;

public class MeetingTemplateTests
{
    [Fact]
    public void All_ContainsFourTemplates()
    {
        Assert.Equal(4, MeetingTemplate.All.Count);
    }

    [Fact]
    public void OneOnOne_HasCorrectName()
    {
        Assert.Equal("1:1", MeetingTemplate.OneOnOne.Name);
        Assert.NotEmpty(MeetingTemplate.OneOnOne.Agenda);
        Assert.NotEmpty(MeetingTemplate.OneOnOne.Objective);
    }

    [Fact]
    public void Standup_HasCorrectName()
    {
        Assert.Equal("Standup", MeetingTemplate.Standup.Name);
        Assert.NotEmpty(MeetingTemplate.Standup.Agenda);
        Assert.NotEmpty(MeetingTemplate.Standup.Objective);
    }

    [Fact]
    public void Review_HasCorrectName()
    {
        Assert.Equal("Review", MeetingTemplate.Review.Name);
        Assert.NotEmpty(MeetingTemplate.Review.Agenda);
        Assert.NotEmpty(MeetingTemplate.Review.Objective);
    }

    [Fact]
    public void Custom_HasEmptyFields()
    {
        Assert.Equal("Custom", MeetingTemplate.Custom.Name);
        Assert.Empty(MeetingTemplate.Custom.Agenda);
        Assert.Empty(MeetingTemplate.Custom.Objective);
    }

    [Fact]
    public void GenerateDefaultTitle_ContainsDateAndTime()
    {
        var title = MeetingTemplate.GenerateDefaultTitle();

        Assert.StartsWith("Reuniao", title);
        Assert.Contains(DateTime.Now.ToString("yyyy-MM-dd"), title);
    }

    [Fact]
    public void GenerateDefaultTitle_CalledTwiceQuickly_ReturnsSameMinute()
    {
        var t1 = MeetingTemplate.GenerateDefaultTitle();
        var t2 = MeetingTemplate.GenerateDefaultTitle();

        Assert.Equal(t1, t2);
    }

    [Fact]
    public void All_OrderIsOneOnOne_Standup_Review_Custom()
    {
        var all = MeetingTemplate.All;
        Assert.Equal("1:1", all[0].Name);
        Assert.Equal("Standup", all[1].Name);
        Assert.Equal("Review", all[2].Name);
        Assert.Equal("Custom", all[3].Name);
    }
}
