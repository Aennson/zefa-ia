# Task 2-6: Configuração de STT Provider

## Descrição
Implementar sistema de configuração que permite trocar entre Whisper local e ElevenLabs via arquivo de config e (futuramente) via UI.

## Skills
- `simplify` — manter configuração mínima
- `security-review` — revisão final do sprint 2

## Dependências
- Tasks 2-2 e 2-5 concluídas

## Entregáveis
- `appsettings.json` com seção de STT
- `STTProviderFactory` que instancia provider correto baseado em config
- Hot-swap: trocar provider sem reiniciar o app (stop → create → start)
- Validação de configuração na inicialização
- Documentação de como configurar cada provider

## Configuração
```json
{
  "STT": {
    "ActiveProvider": "WhisperLocal",
    "WhisperLocal": {
      "ModelSize": "base",
      "Language": "auto",
      "UseGPU": false,
      "ModelPath": "./models"
    },
    "ElevenLabs": {
      "ApiKeyEnvVar": "ELEVENLABS_API_KEY",
      "Language": "auto",
      "VadEnabled": true
    }
  }
}
```

## Critérios de Aceite
- [ ] Config carrega corretamente de `appsettings.json`
- [ ] Factory cria provider correto baseado em `ActiveProvider`
- [ ] Trocar `ActiveProvider` e reiniciar usa novo provider
- [ ] API key de ElevenLabs vem de env var (não do JSON)
- [ ] Config inválida gera erro claro na inicialização
- [ ] Todos os testes do sprint 2 passam

## Testes
- Unit: factory cria WhisperProvider quando config diz "WhisperLocal"
- Unit: factory cria ElevenLabsProvider quando config diz "ElevenLabs"
- Unit: config inválida lança exceção descritiva
- Unit: hot-swap para e inicia novo provider
