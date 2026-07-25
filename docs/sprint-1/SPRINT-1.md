# Sprint 1 — Audio Capture Foundation

## Objetivo
Capturar áudio do microfone e do sistema (loopback) em duas streams separadas, com echo cancellation, e exportar para WAV para verificação manual.

## Entregável
Aplicação console que captura mic + loopback simultaneamente, salva dois arquivos WAV separados, e demonstra que AEC está funcionando.

## Critérios de Aceite
- [ ] Captura de microfone funciona e gera WAV audível
- [ ] Captura de loopback funciona e gera WAV audível
- [ ] Ambas capturas rodam em paralelo sem falhas
- [ ] Echo cancellation remove duplicação do mic
- [ ] Pipeline de áudio emite eventos com chunks de PCM 16kHz
- [ ] Testes unitários passam para todos os componentes

## Tasks
| Task | Descrição | Estimativa |
|------|-----------|------------|
| 1-1 | Setup do projeto (solution, projetos, dependências) | 2h |
| 1-2 | Captura de microfone com NAudio | 3h |
| 1-3 | Captura de loopback WASAPI | 3h |
| 1-4 | Dual-stream capture (paralelo, sincronizado) | 4h |
| 1-5 | Echo cancellation (AEC) | 4h |
| 1-6 | Pipeline de áudio com eventos (IAudioSource) | 3h |

## Dependências Externas
- NAudio (NuGet)
- Windows 10 1903+ com dispositivo de áudio

## Riscos
- Loopback pode não funcionar em VMs sem áudio virtual
- AEC pode exigir WebRTC APM (binding nativo)
