# Testes: SessionExporter

**Arquivo fonte:** `src/ZefaIA.Persistence/SessionExporter.cs`
**Arquivo de teste:** `tests/ZefaIA.Persistence.Tests/SessionExporterTests.cs`
**Classe de teste:** `SessionExporterTests`
**Total:** 24 testes

## Motivacao

A exportacao e o que permite ao usuario tirar dados do app — compartilhar uma ata, arquivar uma reuniao. Os metodos de formatacao sao `internal static` e puros (recebem entidades, devolvem string), o que permite testa-los sem tocar no banco; apenas os quatro testes end-to-end usam o SQLite real.

## Testes

### 1-3. Cabecalho TXT
- `FormatText_IncludesHeaderFields`
- `FormatText_EmptyOptionalFields_OmitsLines`
- `FormatText_EmptyTitle_UsesSessionId`
- **O que testa:** Titulo, data, duracao sempre presentes; participantes/agenda/objetivo aparecem so quando preenchidos (evita linhas "Agenda:" vazias no arquivo). Sem titulo, usa `#<id>`.
- **Execucao:** `dotnet test --filter "SessionExporterTests.FormatText_Includes|SessionExporterTests.FormatText_Empty"`

### 4-6. Corpo TXT
- `FormatText_FormatsTranscriptionLines`
- `FormatText_InlinesSuggestionsBetweenTranscriptions`
- `FormatText_TrailingSuggestions_AppearAtEnd`
- **O que testa:** Linhas no formato `[HH:mm:ss] [Speaker] texto`. Os dois testes de posicionamento verificam a ordem por indice de substring, nao por igualdade do arquivo inteiro — assercoes robustas a mudancas de espacamento. `TrailingSuggestions` cobre o caso da sugestao emitida depois da ultima fala, que sem tratamento explicito seria perdida no fim do loop.
- **Execucao:** `dotnet test --filter "SessionExporterTests.FormatText_Formats|SessionExporterTests.FormatText_Inlines|SessionExporterTests.FormatText_Trailing"`

### 7-9. Formatacao de duracao
- `FormatDuration_Zero_ReturnsEmAndamento`
- `FormatDuration_UnderOneHour_ReturnsMinutes`
- `FormatDuration_OverOneHour_ReturnsHoursAndMinutes`
- **O que testa:** `TimeSpan.Zero` vira "em andamento"; abaixo de 1h usa "45 min"; acima usa "2h 15min"
- **Execucao:** `dotnet test --filter "SessionExporterTests.FormatDuration"`

### 10-12. Saida JSON
- `FormatJson_ProducesParseableJson`
- `FormatJson_IncludesTranscriptionsAndSuggestions`
- `FormatJson_NullEndedAt_SerializesAsNull`
- **O que testa:** O JSON gerado e valido (`JsonDocument.Parse` sem excecao) e contem os campos esperados, incluindo `DurationSeconds` calculado e os contadores de token. `EndedAt` nulo serializa como `null` JSON e nao como string vazia — mantem o arquivo deserializavel por consumidores externos.
- **Execucao:** `dotnet test --filter "SessionExporterTests.FormatJson"`

### 13-16. Intercalacao da linha do tempo
- `Interleave_OrdersByTimestamp`
- `Interleave_NoSuggestions_ReturnsOnlyTranscriptions`
- `Interleave_NoTranscriptions_ReturnsOnlySuggestions`
- `Interleave_BothEmpty_ReturnsEmpty`
- **O que testa:** `Interleave` faz o merge das duas listas ordenadas por `Timestamp`, reordenando internamente para nao depender da ordem de entrada. Os tres casos degenerados (so sugestoes, so transcricoes, ambos vazios) cobrem os limites do merge — uma reuniao onde ninguem falou ainda gera arquivo valido.
- **Execucao:** `dotnet test --filter "SessionExporterTests.Interleave"`

### 17-20. Nome de arquivo sugerido
- `SuggestFileName_Text_UsesTxtExtension`
- `SuggestFileName_Json_UsesJsonExtension`
- `SuggestFileName_EmptyTitle_UsesSessionId`
- `SuggestFileName_InvalidChars_AreReplaced`
- **O que testa:** Formato `<titulo>_<yyyy-MM-dd>.<ext>`. O teste de caracteres invalidos e o mais importante: um titulo como `"Q3: budget/review"` produziria um caminho invalido no Windows e faria o dialogo "Salvar como" falhar. A assercao usa `Path.GetInvalidFileNameChars()` em vez de uma lista fixa.
- **Execucao:** `dotnet test --filter "SessionExporterTests.SuggestFileName"`

### 21-24. End-to-end com SQLite
- `ExportToTextAsync_LoadsFromRepository`
- `ExportToJsonAsync_LoadsFromRepository`
- `ExportToFileAsync_WritesFile`
- `ExportToTextAsync_MissingSession_Throws`
- **O que testa:** O caminho completo — gravar no banco, ler de volta, formatar e escrever em disco. `ExportToFileAsync` grava UTF-8 **sem BOM** e o teste le o arquivo de volta para confirmar o conteudo. Sessao inexistente lanca `InvalidOperationException` em vez de gerar arquivo vazio.
- **Execucao:** `dotnet test --filter "SessionExporterTests.ExportTo"`

## Nota sobre serializacao

O JSON usa `JsonSerializerContext` gerado em tempo de compilacao (`ExportJsonContext`), consistente com o `LLMJsonContext` do Sprint 4. Isso evita reflection em runtime e mantem o projeto compativel com trimming/AOT caso o installer do Sprint 6 use publicacao self-contained.
