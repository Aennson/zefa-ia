# Task 4-4: Hotkey Trigger Global

## Descrição
Implementar trigger por atalho de teclado global (funciona mesmo com o app em background) para solicitar sugestão do LLM sob demanda.

## Skills
- `simplify` — manter implementação direta

## Dependências
- Task 3-6 (hotkeys configuráveis)

## Entregáveis
- `HotkeyTrigger : ITriggerStrategy` em `ZefaIA.Core`
- Registro de hotkey global via Win32 `RegisterHotKey`
- Default: Ctrl+Shift+Space
- Feedback visual no overlay quando hotkey é pressionada
- Suporte a múltiplos hotkeys (sugestão, toggle overlay, copiar)

## Win32
```csharp
[DllImport("user32.dll")]
static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll")]
static extern bool UnregisterHotKey(IntPtr hWnd, int id);
```

## Critérios de Aceite
- [ ] Hotkey funciona com qualquer app em foreground
- [ ] Dispara evento `Triggered` com `Reason = Hotkey`
- [ ] Feedback visual no overlay ("Gerando sugestão...")
- [ ] Não conflita com hotkeys do sistema/outros apps
- [ ] Hotkey é configurável via Settings
- [ ] UnregisterHotKey no Dispose

## Testes
- Unit: registro e desregistro de hotkey
- Unit: WM_HOTKEY dispara evento correto
- Manual: pressionar hotkey com Teams em foreground — trigger funciona
- Manual: trocar hotkey via settings — novo atalho funciona
