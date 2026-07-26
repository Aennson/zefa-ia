namespace ZefaIA.Overlay;

public record MeetingTemplate(
    string Name,
    string Agenda,
    string Objective,
    string Participants
)
{
    public static readonly MeetingTemplate OneOnOne = new(
        "1:1",
        "Acompanhamento individual, feedback, bloqueios",
        "Alinhar expectativas e remover impedimentos",
        ""
    );

    public static readonly MeetingTemplate Standup = new(
        "Standup",
        "O que fiz ontem, o que farei hoje, bloqueios",
        "Sincronizar progresso do time",
        ""
    );

    public static readonly MeetingTemplate Review = new(
        "Review",
        "Revisar entregas do sprint, demonstrar features",
        "Validar que os entregaveis atendem aos criterios de aceite",
        ""
    );

    public static readonly MeetingTemplate Custom = new(
        "Custom",
        "",
        "",
        ""
    );

    public static IReadOnlyList<MeetingTemplate> All { get; } = new[]
    {
        OneOnOne, Standup, Review, Custom
    };

    public static string GenerateDefaultTitle()
    {
        return $"Reuniao {DateTime.Now:yyyy-MM-dd HH:mm}";
    }
}
