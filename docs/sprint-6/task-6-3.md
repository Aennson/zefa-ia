# Task 6-3: Error Handling e Resilience

## Descrição
Implementar tratamento de erros robusto em todo o pipeline, com recovery automático e feedback visual ao usuário.

## Skills
- `security-review` — garantir que erros não expõem dados sensíveis
- `simplify` — manter error handling proporcional

## Dependências
- Task 6-1 (app integrado)

## Entregáveis
- Global exception handler
- Recovery automático por componente
- Notificação visual de erros no overlay
- Log estruturado (Serilog ou similar)
- Crash report local (não enviado)

## Cenários de Erro
| Cenário | Recovery |
|---------|----------|
| Mic desconectado | Retry 3x, avisar no overlay |
| Loopback sem áudio | Continuar só com mic |
| STT timeout | Retry, switch para outro provider |
| Claude API 429 | Backoff exponencial |
| Claude API 500 | Retry 3x, desabilitar trigger temporariamente |
| SQLite locked | Retry com backoff |
| Overlay crash | Recriar janela |

## Critérios de Aceite
- [ ] Nenhum erro causa crash do app
- [ ] Erros transientes são retried automaticamente
- [ ] Usuário é informado de erros persistentes
- [ ] Logs são escritos sem dados sensíveis
- [ ] Recovery funciona sem intervenção manual
- [ ] App sobrevive a desconexão de rede por 5 minutos

## Testes
- Unit: cada cenário de erro é handled corretamente
- Integration: desconectar mic durante captura — recovery
- Integration: simular 429 do Claude — backoff funciona
- Manual: usar por 1h e verificar logs por erros silenciosos
