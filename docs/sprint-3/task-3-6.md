# Task 3-6: Settings UI Básica

## Descrição
Criar janela de configurações para: provedor de STT, perfil do usuário, atalhos de teclado, e preferências de overlay.

## Skills
- `artifact-design` — design da UI de settings
- `simplify` — manter settings mínimos
- `security-review` — revisão final do sprint 3

## Dependências
- Tasks 3-1 a 3-5 (overlay funcional), Task 2-6 (config de STT)

## Entregáveis
- Janela de Settings (WPF Window separada)
- Seções: STT Provider, Perfil, Atalhos, Overlay
- Persistência em `appsettings.json`
- Acesso via system tray ou hotkey

## Seções de Configuração

### STT Provider
- Dropdown: Whisper Local / ElevenLabs
- Whisper: modelo (tiny/base/small), GPU toggle
- ElevenLabs: campo para API key (masked)

### Perfil do Usuário
- Nome
- Cargo/Função
- Área de expertise
- Tom preferido (formal/casual)
- Texto livre para contexto adicional

### Atalhos
- Hotkey para pedir sugestão (default: Ctrl+Shift+Space)
- Hotkey para toggle overlay (default: Ctrl+Shift+Z)
- Hotkey para copiar última sugestão (default: Ctrl+Shift+C)

### Overlay
- Opacidade (slider)
- Tamanho da fonte
- Posição default (dropdown: cantos + custom)
- Auto-hide após N segundos

## Critérios de Aceite
- [ ] Settings abre como janela separada
- [ ] Todas as seções são editáveis
- [ ] Salvar persiste em `appsettings.json`
- [ ] Mudança de STT provider faz hot-swap
- [ ] API key é masked e salva de forma segura
- [ ] Atalhos são registráveis e funcionam globalmente

## Testes
- Manual: abrir settings, mudar valores, salvar, reabrir — valores persistidos
- Manual: trocar STT provider e verificar que transcrição continua funcionando
- Unit: serialização de settings para JSON
- Unit: validação de hotkeys (não conflitar com sistema)
