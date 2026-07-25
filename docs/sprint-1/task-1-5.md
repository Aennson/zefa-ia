# Task 1-5: Echo Cancellation (AEC)

## Descrição
Implementar cancelamento de eco para remover o áudio do loopback que vaza no microfone. Sem AEC, o STT recebe falas duplicadas.

## Skills
- `simplify` — revisar código após implementação
- `security-review` — revisar pipeline completa

## Dependências
- Task 1-4 concluída (dual-stream funcionando)

## Entregáveis
- `EchoCanceller` em `ZefaIA.Audio`
- Integração com o `AudioCaptureEngine`
- Configuração de sensibilidade

## Abordagens (em ordem de preferência)
1. **WebRTC APM via binding .NET** — `WebRtcVadSharp` ou port direto. Melhor qualidade.
2. **Windows AudioClient AEC** — nativo, mas API complexa e inconsistente entre hardware.
3. **Spectral subtraction simplificado** — fallback se os acima falharem. Qualidade razoável.

## Detalhes Técnicos
- O loopback é o sinal de referência (o que sabemos que está tocando)
- O mic é o sinal com eco (voz + eco do loopback)
- AEC = mic - (loopback * filtro adaptativo)
- Latência entre loopback e eco no mic varia (depende do hardware): tipicamente 20-100ms
- Precisa de alinhamento temporal antes de subtrair

## Critérios de Aceite
- [ ] Eco removido de forma perceptível (comparação A/B com WAV)
- [ ] Voz do usuário preservada sem distorção significativa
- [ ] Latência do processamento AEC < 20ms
- [ ] Funciona com diferentes volumes de alto-falante
- [ ] Graceful degradation: se AEC falhar, áudio passa sem processamento (não trava)

## Testes
- Unit: alinhamento temporal funciona com delays conhecidos
- Unit: sinal de referência é subtraído corretamente (sinal sintético)
- Manual: gravar reunião simulada (YouTube + mic), comparar WAV com e sem AEC
- Performance: processar 1 minuto de áudio em < 1s
