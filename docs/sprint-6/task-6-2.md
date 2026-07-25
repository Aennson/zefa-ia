# Task 6-2: System Tray

## Descrição
Implementar ícone na system tray com menu de contexto para controle do app sem precisar de janela principal.

## Skills
- `simplify` — manter menu conciso

## Dependências
- Task 6-1 (app integrado)

## Entregáveis
- `NotifyIcon` na system tray com ícone do Zefa IA
- Menu de contexto: Nova Reunião, Parar, Settings, Histórico, Sair
- Indicadores de estado no ícone (idle, gravando, erro)
- Double-click abre overlay/settings
- Tooltip com estado atual

## Menu de Contexto
```
🎙️ Nova Reunião
⏹️ Parar Reunião
─────────
⚙️ Configurações
📋 Histórico
─────────
❌ Sair
```

## Critérios de Aceite
- [ ] Ícone aparece na system tray
- [ ] Menu de contexto funciona
- [ ] Estado visual muda (idle vs gravando)
- [ ] Double-click abre funcionalidade principal
- [ ] "Sair" faz shutdown graceful
- [ ] App minimiza para tray ao fechar janela principal

## Testes
- Manual: right-click mostra menu, itens funcionam
- Manual: ícone muda ao iniciar/parar reunião
- Manual: fechar janela → app continua no tray
