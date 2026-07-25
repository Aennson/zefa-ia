# Task 3-2: SetWindowDisplayAffinity (Excluir de Capture)

## Descrição
Aplicar `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` para que o overlay não apareça em screenshots, screen recordings, ou compartilhamento de tela (Teams, Zoom, Meet, OBS).

## Skills
- `security-review` — garantir que exclusão funciona corretamente

## Dependências
- Task 3-1 concluída (overlay window existe)

## Entregáveis
- Aplicação de `WDA_EXCLUDEFROMCAPTURE` na inicialização do overlay
- Toggle para habilitar/desabilitar (debug: ver overlay no capture)
- Fallback para `WDA_MONITOR` em versões antigas do Windows

## Win32
```csharp
[DllImport("user32.dll")]
static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

const uint WDA_NONE = 0x00000000;
const uint WDA_MONITOR = 0x00000001;           // Win10 1903+
const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011; // Win10 2004+
```

## Compatibilidade
- `WDA_EXCLUDEFROMCAPTURE` (Win10 2004+): overlay some completamente do capture
- `WDA_MONITOR` (Win10 1903+): overlay aparece como área preta no capture (fallback)
- Abaixo de 1903: sem suporte — avisar o usuário

## Critérios de Aceite
- [ ] Overlay NÃO aparece em screenshot (Win+Shift+S)
- [ ] Overlay NÃO aparece em compartilhamento do Teams/Zoom
- [ ] Overlay NÃO aparece em gravação do OBS
- [ ] Overlay APARECE na tela real do usuário
- [ ] Toggle funciona: desabilitar mostra overlay no capture
- [ ] Fallback graceful em Windows mais antigo

## Testes
- Manual: screenshot com Win+Shift+S — overlay deve estar ausente
- Manual: compartilhar tela no Teams — overlay deve estar ausente
- Manual: toggle off — overlay aparece no capture
- Unit: API call é feita com parâmetro correto
