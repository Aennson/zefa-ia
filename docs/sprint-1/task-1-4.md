# Task 1-4: Dual-Stream Capture

## Descrição
Orquestrar captura simultânea de microfone e loopback, mantendo streams sincronizadas com timestamps relativos ao início da sessão.

## Skills
- `simplify` — revisar código após implementação

## Dependências
- Task 1-2 e 1-3 concluídas

## Entregáveis
- `AudioCaptureEngine` em `ZefaIA.Audio` que gerencia ambas as fontes
- Sincronização por timestamp (relativo ao start da sessão)
- Stream unificada de eventos com identificação de origem (mic vs loopback)
- Exportação WAV dual-track para verificação
- App console de teste

## Interface
```csharp
public class AudioCaptureEngine : IDisposable
{
    public IObservable<AudioChunkEventArgs> AudioStream { get; }
    
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    
    IReadOnlyList<IAudioSource> ActiveSources { get; }
}
```

## Critérios de Aceite
- [ ] Mic e loopback iniciam/param juntos
- [ ] Timestamps são relativos ao mesmo ponto zero
- [ ] Falha em uma fonte não derruba a outra
- [ ] Eventos chegam com `AudioSourceType` correto
- [ ] App console demonstra captura dual por 30s
- [ ] Dois WAVs gerados são audíveis e distintos

## Testes
- Unit: engine inicia e para ambas as fontes
- Unit: falha em uma fonte emite erro sem parar a outra
- Unit: timestamps são monotônicos e relativos ao start
- Integration: captura real de 10s, verifica WAVs gerados
