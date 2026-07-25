# Task 2-1: Abstração ISTTProvider e Modelos

## Descrição
Definir a interface de abstração para provedores de STT e os modelos de dados de transcrição. Esta interface será implementada por Whisper (task 2-2) e ElevenLabs (task 2-5).

## Skills
- `simplify` — garantir que a interface é mínima e limpa

## Dependências
- Task 1-1 (projeto ZefaIA.Core existe)

## Entregáveis
- `ISTTProvider` em `ZefaIA.Core`
- Modelos: `TranscriptionSegment`, `TranscriptionResult`, `STTProviderConfig`
- Enum `STTProviderType` (WhisperLocal, ElevenLabs)
- Factory: `STTProviderFactory`

## Interface
```csharp
public interface ISTTProvider : IAsyncDisposable
{
    string ProviderId { get; }
    STTProviderType Type { get; }
    IReadOnlyList<string> SupportedLanguages { get; }
    
    event EventHandler<TranscriptionSegmentEventArgs> SegmentReceived;
    event EventHandler<TranscriptionSegmentEventArgs> PartialReceived;
    
    Task InitializeAsync(STTProviderConfig config, CancellationToken ct = default);
    Task ProcessAudioAsync(AudioChunkEventArgs chunk, CancellationToken ct = default);
    Task FlushAsync(); // Force process any buffered audio
}

public record TranscriptionSegment(
    string Text,
    string Language,        // detected language code
    float Confidence,
    TimeSpan StartTime,
    TimeSpan EndTime,
    AudioSourceType Source, // Microphone or Loopback
    bool IsFinal            // false = partial, true = committed
);
```

## Critérios de Aceite
- [ ] Interface é implementável tanto para streaming (ElevenLabs) quanto batch (Whisper)
- [ ] Modelos cobrem: texto, idioma, confidence, timestamps, speaker source
- [ ] Factory cria provider correto baseado em config
- [ ] Build compila sem erros

## Testes
- Unit: factory cria provider correto por tipo
- Unit: modelos serializam/deserializam corretamente
