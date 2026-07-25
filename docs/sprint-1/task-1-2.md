# Task 1-2: Captura de Microfone com NAudio

## Descrição
Implementar captura de áudio do microfone usando NAudio `WaveInEvent`, com resampling para PCM 16kHz mono (formato esperado pelo STT).

## Skills
- `simplify` — revisar código após implementação
- `security-review` — verificar que não há vazamento de recursos de áudio

## Dependências
- Task 1-1 concluída (projeto existe)

## Entregáveis
- `MicrophoneSource : IAudioSource` em `ZefaIA.Audio`
- Enum de dispositivos disponíveis
- Resampling para PCM 16kHz 16-bit mono
- Eventos: `OnAudioChunk`, `OnStarted`, `OnStopped`, `OnError`
- Testes unitários com mock de `WaveInEvent`

## Interface Base (definida em ZefaIA.Core)
```csharp
public interface IAudioSource : IDisposable
{
    string SourceId { get; }
    string DisplayName { get; }
    AudioSourceType Type { get; } // Microphone, Loopback
    
    event EventHandler<AudioChunkEventArgs> AudioChunkReceived;
    event EventHandler<AudioSourceStateEventArgs> StateChanged;
    
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
}

public record AudioChunkEventArgs(
    byte[] PcmData,      // PCM 16-bit mono
    int SampleRate,       // 16000
    TimeSpan Timestamp,
    AudioSourceType Source
);
```

## Critérios de Aceite
- [ ] Lista dispositivos de microfone disponíveis
- [ ] Captura áudio do mic default
- [ ] Output é PCM 16kHz 16-bit mono
- [ ] Eventos disparam corretamente
- [ ] Dispose libera recursos do NAudio
- [ ] Funciona por pelo menos 5 minutos sem memory leak

## Testes
- Unit: mock do WaveInEvent, verifica dispatch de eventos
- Unit: resampling produz formato correto
- Unit: dispose libera recursos
- Manual: gravar 10s do mic, salvar WAV, ouvir
