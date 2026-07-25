# Task 1-3: Captura de Loopback WASAPI

## Descrição
Implementar captura do áudio do sistema (o que sai nos alto-falantes) usando NAudio `WasapiLoopbackCapture`, com resampling para PCM 16kHz mono.

## Skills
- `simplify` — revisar código após implementação
- `security-review` — verificar que não há vazamento de recursos

## Dependências
- Task 1-2 concluída (IAudioSource definida e padrão estabelecido)

## Entregáveis
- `LoopbackSource : IAudioSource` em `ZefaIA.Audio`
- Enum de dispositivos de saída disponíveis
- Resampling para PCM 16kHz 16-bit mono (loopback geralmente vem em 48kHz float32)
- Mesma interface de eventos que MicrophoneSource
- Testes unitários

## Detalhes Técnicos
- `WasapiLoopbackCapture` captura em formato float32 no sample rate do dispositivo (geralmente 48kHz stereo)
- Precisa converter: float32 → int16, 48kHz → 16kHz, stereo → mono
- Pipeline: `WasapiLoopbackCapture` → `WaveFloatTo16` → `MediaFoundationResampler` ou custom downsampler
- Loopback requer que haja pelo menos um dispositivo de saída ativo

## Critérios de Aceite
- [ ] Captura áudio do sistema (testar com YouTube/Spotify)
- [ ] Output é PCM 16kHz 16-bit mono (mesmo formato que mic)
- [ ] Conversão float32 → int16 sem clipping
- [ ] Downsampling 48kHz → 16kHz sem artefatos audíveis
- [ ] Funciona com diferentes sample rates de dispositivo
- [ ] Dispose libera recursos WASAPI corretamente

## Testes
- Unit: conversão de formato produz output correto
- Unit: dispose libera recursos
- Manual: capturar áudio de um vídeo do YouTube, salvar WAV, comparar qualidade
