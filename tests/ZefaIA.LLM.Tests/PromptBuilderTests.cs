using Xunit;

namespace ZefaIA.LLM.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void Build_WithoutProfile_ReturnsCorePrompt()
    {
        var prompt = new PromptBuilder().Build();

        Assert.Contains("Zefa", prompt);
        Assert.Contains("[SEM SUGESTAO]", prompt);
        Assert.DoesNotContain("Perfil do Usuario", prompt);
        Assert.DoesNotContain("Contexto da Reuniao", prompt);
    }

    [Fact]
    public void Build_WithProfile_IncludesUserInfo()
    {
        var prompt = new PromptBuilder()
            .WithProfile("Ana", "Tech Lead", "Backend .NET", "Direto")
            .Build();

        Assert.Contains("Perfil do Usuario", prompt);
        Assert.Contains("Nome: Ana", prompt);
        Assert.Contains("Cargo: Tech Lead", prompt);
        Assert.Contains("Expertise: Backend .NET", prompt);
        Assert.Contains("Tom preferido: Direto", prompt);
    }

    [Fact]
    public void Build_WithMeetingContext_IncludesMeetingInfo()
    {
        var prompt = new PromptBuilder()
            .WithMeetingContext(
                agenda: "Sprint planning",
                objective: "Definir tasks do Sprint 5",
                participants: "Ana (TL), Carlos (PM), Joao (Dev)")
            .Build();

        Assert.Contains("Contexto da Reuniao", prompt);
        Assert.Contains("Pauta: Sprint planning", prompt);
        Assert.Contains("Objetivo: Definir tasks do Sprint 5", prompt);
        Assert.Contains("Participantes: Ana (TL), Carlos (PM), Joao (Dev)", prompt);
    }

    [Fact]
    public void Build_WithProfileAndMeeting_IncludesBoth()
    {
        var prompt = new PromptBuilder()
            .WithProfile("Ana", "Tech Lead", "Backend")
            .WithMeetingContext(agenda: "Retro", objective: "Melhorar processo")
            .Build();

        Assert.Contains("Perfil do Usuario", prompt);
        Assert.Contains("Contexto da Reuniao", prompt);
        Assert.Contains("Nome: Ana", prompt);
        Assert.Contains("Pauta: Retro", prompt);
    }

    [Fact]
    public void Build_WithAdditionalContext_IncludesIt()
    {
        var prompt = new PromptBuilder()
            .WithProfile("Ana", "TL", "", additionalContext: "Prefiro exemplos com codigo")
            .Build();

        Assert.Contains("Contexto adicional: Prefiro exemplos com codigo", prompt);
    }

    [Fact]
    public void Build_WithEmptyMeetingContext_OmitsMeetingSection()
    {
        var prompt = new PromptBuilder()
            .WithProfile("Ana", "Dev", "")
            .WithMeetingContext(agenda: "", objective: "", participants: "")
            .Build();

        Assert.Contains("Perfil do Usuario", prompt);
        Assert.DoesNotContain("Contexto da Reuniao", prompt);
    }

    [Fact]
    public void Build_CorePromptContainsRules()
    {
        var prompt = new PromptBuilder().Build();

        Assert.Contains("2-3 frases", prompt);
        Assert.Contains("contra-argumentos", prompt);
        Assert.Contains("[SEM SUGESTAO]", prompt);
        Assert.Contains("acionaveis", prompt);
    }

    [Fact]
    public void Build_CorePromptContainsEmojiPrefixes()
    {
        var prompt = new PromptBuilder().Build();

        Assert.Contains("Sugestao", prompt);
        Assert.Contains("Risco", prompt);
        Assert.Contains("Dado", prompt);
        Assert.Contains("Proximo passo", prompt);
        Assert.Contains("Contra-argumento", prompt);
    }

    [Fact]
    public void Build_PromptUnder4000Tokens()
    {
        var prompt = new PromptBuilder()
            .WithProfile("Ana Silva", "Senior Tech Lead", "Distributed Systems, .NET, AWS",
                "Formal e direto", "10 anos de experiencia em fintech")
            .WithMeetingContext(
                "Revisao de arquitetura do novo sistema de pagamentos",
                "Validar proposta de migracao para microservicos",
                "Ana (TL), Carlos (Arquiteto), Maria (PM), Pedro (DevOps)")
            .Build();

        var estimatedTokens = prompt.Length / 4;
        Assert.True(estimatedTokens < 4000, $"Prompt too long: ~{estimatedTokens} tokens");
    }

    [Fact]
    public void Build_IsIdempotent()
    {
        var builder = new PromptBuilder()
            .WithProfile("Ana", "Dev", "C#");

        var first = builder.Build();
        var second = builder.Build();

        Assert.Equal(first, second);
    }
}
