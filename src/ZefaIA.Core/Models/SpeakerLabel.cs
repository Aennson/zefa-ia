namespace ZefaIA.Core.Models;

public record SpeakerLabel(
    AudioSourceType Source,
    string DisplayName
)
{
    public static SpeakerLabel Me(string name = "Eu") => new(AudioSourceType.Microphone, name);
    public static SpeakerLabel Other(string name = "Interlocutor") => new(AudioSourceType.Loopback, name);
}
