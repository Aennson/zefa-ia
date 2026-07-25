# Task 2-5: ElevenLabs STT Provider

## Descrição
Implementar `ISTTProvider` usando ElevenLabs Scribe v2 Realtime via WebSocket. Plugável e alternável com Whisper via configuração.

## Skills
- `claude-api` — referência de integração com APIs externas
- `simplify` — revisar código após implementação
- `security-review` — garantir que API key é tratada com segurança

## Dependências
- Task 2-1 concluída (interface ISTTProvider)

## Entregáveis
- `ElevenLabsSTTProvider : ISTTProvider` em `ZefaIA.STT`
- Conexão WebSocket com reconexão automática
- Streaming de áudio PCM 16kHz
- Recepção de transcrições parciais e committed
- Tratamento seguro de API key (não logar, não persistir em plaintext)

## Protocolo ElevenLabs Scribe Realtime
- Endpoint: `wss://api.elevenlabs.io/v1/speech-to-text/realtime`
- Auth: header `xi-api-key`
- Envio: chunks de áudio base64-encoded em JSON
- Recepção: objetos JSON com `type: "transcript"`, `is_final`, `text`, `language`
- VAD automático do server (configurável)

## Formato de Mensagem
```json
// Envio
{"audio": "<base64 PCM>", "sample_rate": 16000}

// Recepção parcial
{"type": "transcript", "is_final": false, "text": "olá tudo", "language": "pt"}

// Recepção final
{"type": "transcript", "is_final": true, "text": "olá tudo bem", "language": "pt", "confidence": 0.95}
```

## Critérios de Aceite
- [ ] Conecta ao WebSocket do ElevenLabs
- [ ] Envia áudio e recebe transcrições
- [ ] Reconecta automaticamente em caso de disconnect
- [ ] API key lida de variável de ambiente ou config segura
- [ ] Funciona como drop-in replacement do WhisperProvider
- [ ] Graceful degradation: se WebSocket falhar, emite erro sem crashar

## Testes
- Unit: serialização de mensagens no formato correto
- Unit: parsing de respostas parciais e finais
- Unit: reconexão é tentada após disconnect
- Integration: (requer API key) transcrever áudio de teste via WebSocket
