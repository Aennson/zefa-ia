# Testes: SimpleMarkdownParser

**Arquivo fonte:** `src/ZefaIA.Overlay/SimpleMarkdownParser.cs`
**Arquivo de teste:** `tests/ZefaIA.Overlay.Tests/SimpleMarkdownParserTests.cs`
**Classe de teste:** `SimpleMarkdownParserTests`

## Motivacao

`SimpleMarkdownParser` converte markdown basico (bold, italico, listas) para WPF `Inline` elements. Usado para renderizar sugestoes do LLM no overlay.

## Testes

### 1. `Parse_PlainText_CreatesTextBlock`
- **Tipo:** Unit
- **O que testa:** Texto puro gera TextBlock com TextWrapping
- **Execucao:** `dotnet test --filter "SimpleMarkdownParserTests.Parse_PlainText_CreatesTextBlock"`

### 2. `Parse_BoldText_CreatesBoldRun`
- **Tipo:** Unit
- **O que testa:** `**bold**` gera Run com FontWeight.Bold
- **Execucao:** `dotnet test --filter "SimpleMarkdownParserTests.Parse_BoldText_CreatesBoldRun"`

### 3. `Parse_ItalicText_CreatesItalicRun`
- **Tipo:** Unit
- **O que testa:** `*italic*` gera Run com FontStyle.Italic
- **Execucao:** `dotnet test --filter "SimpleMarkdownParserTests.Parse_ItalicText_CreatesItalicRun"`

### 4. `Parse_BulletList_AddsBulletPrefix`
- **Tipo:** Unit
- **O que testa:** `- Item` gera prefixo bullet
- **Execucao:** `dotnet test --filter "SimpleMarkdownParserTests.Parse_BulletList_AddsBulletPrefix"`

### 5. `Parse_Multiline_AddsLineBreaks`
- **Tipo:** Unit
- **O que testa:** Quebras de linha sao preservadas
- **Execucao:** `dotnet test --filter "SimpleMarkdownParserTests.Parse_Multiline_AddsLineBreaks"`

### 6. `Parse_EmptyString_ReturnsEmptyTextBlock`
- **Tipo:** Unit
- **O que testa:** String vazia nao causa crash
- **Execucao:** `dotnet test --filter "SimpleMarkdownParserTests.Parse_EmptyString_ReturnsEmptyTextBlock"`

### 7. `Parse_CustomFontSize_Applied`
- **Tipo:** Unit
- **O que testa:** FontSize customizado e aplicado
- **Execucao:** `dotnet test --filter "SimpleMarkdownParserTests.Parse_CustomFontSize_Applied"`
