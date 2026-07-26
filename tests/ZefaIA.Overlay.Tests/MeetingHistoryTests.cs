using Xunit;
using ZefaIA.Overlay;
using ZefaIA.Persistence;

namespace ZefaIA.Overlay.Tests;

public class MeetingHistoryTests
{
    [Fact]
    public void SessionListItem_From_MapsAllFields()
    {
        var session = new MeetingSession
        {
            Id = 42,
            Title = "Sprint Review",
            StartedAt = new DateTime(2025, 3, 15, 14, 30, 0),
            EndedAt = new DateTime(2025, 3, 15, 15, 0, 0),
            Participants = "Alice, Bob"
        };

        var item = SessionListItem.From(session);

        Assert.Equal(42, item.SessionId);
        Assert.Equal("Sprint Review", item.Title);
        Assert.Equal("15/03/2025 14:30", item.DateDisplay);
        Assert.Equal("30min", item.DurationDisplay);
        Assert.Equal("Alice, Bob", item.Participants);
    }

    [Fact]
    public void SessionListItem_From_EmptyTitle_ShowsFallback()
    {
        var session = new MeetingSession { Id = 7, Title = "" };
        var item = SessionListItem.From(session);

        Assert.Equal("Reuniao #7", item.Title);
    }

    [Fact]
    public void SessionListItem_From_WhitespaceTitle_ShowsFallback()
    {
        var session = new MeetingSession { Id = 3, Title = "   " };
        var item = SessionListItem.From(session);

        Assert.Equal("Reuniao #3", item.Title);
    }

    [Fact]
    public void SessionListItem_From_NoEndedAt_ShowsEmAndamento()
    {
        var session = new MeetingSession
        {
            Id = 1,
            StartedAt = DateTime.UtcNow,
            EndedAt = null
        };

        var item = SessionListItem.From(session);
        Assert.Equal("Em andamento", item.DurationDisplay);
    }

    [Fact]
    public void SessionListItem_From_NoParticipants_ShowsDash()
    {
        var session = new MeetingSession { Id = 1, Participants = "" };
        var item = SessionListItem.From(session);

        Assert.Equal("-", item.Participants);
    }

    [Fact]
    public void SessionListItem_From_LongDuration_ShowsMinutes()
    {
        var session = new MeetingSession
        {
            Id = 1,
            StartedAt = new DateTime(2025, 1, 1, 10, 0, 0),
            EndedAt = new DateTime(2025, 1, 1, 12, 15, 0)
        };

        var item = SessionListItem.From(session);
        Assert.Equal("135min", item.DurationDisplay);
    }
}
