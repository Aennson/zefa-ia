# Task 4-3: Silence Detection Trigger (VAD)

## Descrição
Implementar detecção de silêncio no stream de áudio do loopback para disparar automaticamente pedidos de sugestão ao LLM quando o interlocutor para de falar.

## Skills
- `simplify` — manter detector simples e eficiente

## Dependências
- Task 1-6 (pipeline de áudio), Task 2-3 (transcrição rodando)

## Entregáveis
- `SilenceTrigger : ITriggerStrategy` em `ZefaIA.Core`
- Detecção de silêncio baseada em energia RMS do áudio
- Configuração: threshold de silêncio (default 1.5s), sensibilidade
- Cooldown entre triggers (evitar spam)
- Integração com AudioCaptureEngine

## Interface
```csharp
public interface ITriggerStrategy
{
    string TriggerName { get; }
    event EventHandler<TriggerEventArgs> Triggered;
    
    Task StartMonitoringAsync(CancellationToken ct = default);
    Task StopMonitoringAsync();
}

public record TriggerEventArgs(
    string TriggerName,
    TriggerReason Reason,       // Silence, Hotkey, Manual
    TimeSpan TranscriptWindow,  // quanto de transcrição incluir
    DateTime Timestamp
);
```

## Algoritmo
```
1. Monitorar RMS do stream de loopback
2. Se RMS < threshold por > 1.5s consecutivos:
   a. Verificar se há transcrição recente (últimos 30s)
   b. Verificar cooldown (não disparar se último trigger foi < 10s atrás)
   c. Disparar evento Triggered
3. Reset timer quando RMS > threshold
```

## Critérios de Aceite
- [ ] Detecta silêncio de 1.5s+ no loopback
- [ ] Não dispara durante fala ativa
- [ ] Cooldown previne spam (configurável, default 10s)
- [ ] Não dispara se não há transcrição recente
- [ ] Threshold de silêncio é configurável
- [ ] Latência da detecção < 200ms após silêncio real

## Testes
- Unit: RMS abaixo do threshold por tempo configurado dispara evento
- Unit: RMS acima do threshold reseta o timer
- Unit: cooldown previne trigger consecutivo
- Unit: sem transcrição recente não dispara
- Performance: processamento de RMS não adiciona latência perceptível
