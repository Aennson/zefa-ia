# Task 6-1: Integração End-to-End

## Descrição
Conectar todos os componentes dos sprints anteriores em um fluxo coeso: app startup → config → meeting start → audio capture → STT → LLM → overlay → persist.

## Skills
- `simplify` — eliminar código morto e simplificar wiring
- `run` — testar fluxo completo

## Dependências
- Sprints 1-5 concluídos

## Entregáveis
- `App.xaml.cs` com DI container configurado
- Startup sequence orquestrada
- Shutdown graceful (parar tudo em ordem)
- Fluxo testado end-to-end

## Startup Sequence
```
1. Carregar configurações (appsettings.json)
2. Inicializar SQLite
3. Carregar perfil do usuário
4. Registrar DI (audio, STT, LLM, overlay, persistence)
5. Inicializar overlay (hidden)
6. Criar system tray icon
7. Aguardar comando de iniciar reunião
```

## Shutdown Sequence
```
1. Parar triggers
2. Flush sugestões pendentes
3. Parar LLM client
4. Parar STT providers
5. Parar audio capture
6. Flush persistence (último batch)
7. Fechar overlay
8. Dispose DI container
```

## Critérios de Aceite
- [ ] App inicia sem erros
- [ ] Fluxo completo funciona (áudio → texto → sugestão → overlay)
- [ ] Shutdown não perde dados
- [ ] Erro em um componente não derruba os outros
- [ ] DI container está correto (sem circular dependencies)
- [ ] Log de startup mostra estado de cada componente

## Testes
- Integration: startup completo sem erros
- Integration: shutdown graceful sem dados perdidos
- Integration: falha de STT não derruba áudio
- Manual: usar por 15 minutos em reunião simulada
