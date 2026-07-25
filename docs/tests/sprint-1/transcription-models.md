# Testes: TranscriptionModels

**Arquivo fonte:** `src/ZefaIA.Core/Models/TranscriptionModels.cs`
**Arquivo de teste:** `tests/ZefaIA.STT.Tests/TranscriptionModelsTests.cs`
**Classe de teste:** `TranscriptionModelsTests`

## Motivacao

Os models de transcrição (`TranscriptionSegment`, `STTProviderConfig`) sao usados na comunicacao entre o STT provider (Sprint 2) e o restante do sistema. Testar sua construcao garante que os records se comportam como esperado antes da implementacao do STT.

## Testes

### 1. `TranscriptionSegment_CreatesCorrectly`
- **Tipo:** Unit
- **O que testa:** Construcao do record `TranscriptionSegment` com todos os campos
- **Como funciona:** Cria um segmento com texto "Ola, tudo bem?", idioma "pt", confidence 0.95, timestamps, source Loopback e `IsFinal=true`. Verifica cada propriedade.
- **Por que existe:** O `TranscriptionSegment` e o contrato entre STT e o restante do sistema. Se um campo mudar de tipo ou ordem, este teste detecta imediatamente.
- **Execucao:** `dotnet test --filter "TranscriptionModelsTests.TranscriptionSegment_CreatesCorrectly"`

### 2. `STTProviderConfig_DefaultsWork`
- **Tipo:** Unit
- **O que testa:** `STTProviderConfig` inicializa com `Options` como dicionario vazio
- **Como funciona:** Cria config com `ProviderType=WhisperLocal` e `Language="auto"`. Verifica que `Options` esta vazio (nao null) e o tipo esta correto.
- **Por que existe:** Se `Options` inicializasse como null, qualquer acesso sem null-check causaria `NullReferenceException`. O default de dicionario vazio garante seguranca.
- **Execucao:** `dotnet test --filter "TranscriptionModelsTests.STTProviderConfig_DefaultsWork"`
