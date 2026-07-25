# Sprint 2 — Speech-to-Text Pipeline

## Objetivo
Transcrever áudio em tempo real usando Whisper local, com abstração para trocar de provedor. Diarização baseada na origem do stream (mic = "eu", loopback = "eles").

## Entregável
App console que captura áudio dual, transcreve em tempo real com identificação de speaker, e exibe texto no console com timestamps.

## Critérios de Aceite
- [ ] Transcrição funciona em tempo real com Whisper local
- [ ] Diarização por stream funciona (mic vs loopback diferenciados)
- [ ] Interface ISTTProvider permite trocar provedor
- [ ] Latência STT < 1s para frases curtas
- [ ] Detecção de idioma automática (PT-BR e EN mínimo)
- [ ] Provider ElevenLabs implementado (mesmo que não testado sem API key)

## Tasks
| Task | Descrição | Estimativa |
|------|-----------|------------|
| 2-1 | Abstração ISTTProvider e modelos de transcrição | 2h |
| 2-2 | Whisper local provider (whisper.net) | 4h |
| 2-3 | Pipeline de transcrição em tempo real | 4h |
| 2-4 | Diarização por stream (speaker identification) | 3h |
| 2-5 | ElevenLabs provider (plugável) | 3h |
| 2-6 | Configuração de STT provider via settings | 2h |

## Dependências Externas
- Whisper.net (NuGet) ou faster-whisper via subprocess
- Modelo Whisper (base/small — download automático)
- ElevenLabs API key (opcional, para task 2-5)
