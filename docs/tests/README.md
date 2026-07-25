# Zefa IA — Documentacao de Testes

## Estrutura

```
docs/tests/
├── README.md              # Este arquivo
├── sprint-1/
│   ├── audio-models.md
│   ├── resampler.md
│   ├── microphone-source.md
│   ├── loopback-source.md
│   ├── audio-device-enumerator.md
│   ├── audio-capture-engine.md
│   ├── echo-canceller.md
│   ├── audio-pipeline.md
│   ├── wav-exporter.md
│   ├── transcription-models.md
│   └── llm-models.md
├── sprint-2/
├── sprint-3/
...
```

## Convencoes

- Um arquivo `.md` por classe/componente testado
- Cada arquivo lista todos os testes, seu motivo, como sao executados, e o que validam
- Testes marcados com `Skip` indicam dependencia de hardware (Windows audio device)
- Framework: **xUnit** com **Moq** para mocking

## Como Executar

```bash
# Todos os testes
dotnet test

# Testes de um projeto especifico
dotnet test tests/ZefaIA.Audio.Tests

# Testes excluindo os que precisam de hardware
dotnet test --filter "Category!=RequiresHardware"
```

## Categorias de Teste

| Tipo | Descricao | Ambiente |
|------|-----------|----------|
| **Unit** | Testa logica isolada com mocks | Qualquer (CI/local) |
| **Integration** | Testa interacao entre componentes reais | Qualquer (CI/local) |
| **Hardware** | Requer dispositivos de audio Windows | Apenas local com hardware |
| **Manual** | Verificacao humana (ouvir WAV, ver overlay) | Apenas local |
