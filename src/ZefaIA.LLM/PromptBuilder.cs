namespace ZefaIA.LLM;

public sealed class PromptBuilder
{
    private string _userName = "";
    private string _userRole = "";
    private string _userExpertise = "";
    private string _preferredTone = "Formal";
    private string _additionalContext = "";
    private string _meetingAgenda = "";
    private string _meetingObjective = "";
    private string _meetingParticipants = "";

    public PromptBuilder WithProfile(
        string userName,
        string userRole,
        string userExpertise,
        string preferredTone = "Formal",
        string additionalContext = "")
    {
        _userName = userName;
        _userRole = userRole;
        _userExpertise = userExpertise;
        _preferredTone = preferredTone;
        _additionalContext = additionalContext;
        return this;
    }

    public PromptBuilder WithMeetingContext(
        string agenda = "",
        string objective = "",
        string participants = "")
    {
        _meetingAgenda = agenda;
        _meetingObjective = objective;
        _meetingParticipants = participants;
        return this;
    }

    public string Build()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(CorePrompt);

        if (!string.IsNullOrWhiteSpace(_userName) || !string.IsNullOrWhiteSpace(_userRole))
        {
            sb.AppendLine();
            sb.AppendLine("## Perfil do Usuario");
            if (!string.IsNullOrWhiteSpace(_userName))
                sb.AppendLine($"- Nome: {_userName}");
            if (!string.IsNullOrWhiteSpace(_userRole))
                sb.AppendLine($"- Cargo: {_userRole}");
            if (!string.IsNullOrWhiteSpace(_userExpertise))
                sb.AppendLine($"- Expertise: {_userExpertise}");
            if (!string.IsNullOrWhiteSpace(_preferredTone))
                sb.AppendLine($"- Tom preferido: {_preferredTone}");
            if (!string.IsNullOrWhiteSpace(_additionalContext))
                sb.AppendLine($"- Contexto adicional: {_additionalContext}");
        }

        if (HasMeetingContext())
        {
            sb.AppendLine();
            sb.AppendLine("## Contexto da Reuniao");
            if (!string.IsNullOrWhiteSpace(_meetingAgenda))
                sb.AppendLine($"- Pauta: {_meetingAgenda}");
            if (!string.IsNullOrWhiteSpace(_meetingObjective))
                sb.AppendLine($"- Objetivo: {_meetingObjective}");
            if (!string.IsNullOrWhiteSpace(_meetingParticipants))
                sb.AppendLine($"- Participantes: {_meetingParticipants}");
        }

        return sb.ToString().TrimEnd();
    }

    private bool HasMeetingContext() =>
        !string.IsNullOrWhiteSpace(_meetingAgenda) ||
        !string.IsNullOrWhiteSpace(_meetingObjective) ||
        !string.IsNullOrWhiteSpace(_meetingParticipants);

    internal const string CorePrompt = """
        Voce e Zefa, um assistente de reunioes em tempo real. Voce recebe a transcricao ao vivo da reuniao e gera sugestoes contextuais para ajudar o usuario.

        ## Regras
        1. Sugestoes curtas: maximo 2-3 frases.
        2. Foque em: dados relevantes, contra-argumentos, pontos perdidos, riscos, proximos passos.
        3. Nao repita o que ja foi dito na transcricao.
        4. Responda "[SEM SUGESTAO]" quando nao houver nada util a adicionar.
        5. Adapte o idioma ao idioma da transcricao (PT-BR, EN, etc).
        6. Priorize itens acionaveis sobre observacoes genericas.
        7. Use prefixos de emoji para categorizar:
           - 💡 Sugestao / Insight
           - ⚠️ Risco / Atencao
           - 📊 Dado / Metrica
           - ✅ Proximo passo
           - 🔄 Contra-argumento
        """;
}
