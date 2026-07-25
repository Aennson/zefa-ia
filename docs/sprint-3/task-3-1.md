# Task 3-1: Overlay Window Base

## Descrição
Criar a janela WPF base do overlay: topmost, fundo transparente, sem borda, click-through via Win32 extended window styles.

## Skills
- `artifact-design` — design do overlay
- `simplify` — manter código WPF limpo

## Dependências
- Task 1-1 (projeto ZefaIA.Overlay existe)

## Entregáveis
- `OverlayWindow : Window` em `ZefaIA.Overlay`
- Fundo transparente com área de conteúdo semi-transparente
- Click-through via `WS_EX_TRANSPARENT | WS_EX_LAYERED`
- Topmost: sempre acima de outras janelas
- `WS_EX_TOOLWINDOW`: não aparece na taskbar nem no Alt+Tab
- Posição configurável (default: canto inferior direito)
- Resize por drag nas bordas da área de conteúdo

## Win32 Interop
```csharp
// Extended styles para click-through
const int WS_EX_TRANSPARENT = 0x00000020;
const int WS_EX_LAYERED = 0x00080000;
const int WS_EX_TOOLWINDOW = 0x00000080;

// Aplicar via SetWindowLong no SourceInitialized
var hwnd = new WindowInteropHelper(this).Handle;
var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
```

## XAML Base
```xml
<Window AllowsTransparency="True"
        Background="Transparent"
        WindowStyle="None"
        Topmost="True"
        ShowInTaskbar="False">
    <Border Background="#CC1A1A2E" CornerRadius="12" Padding="16">
        <!-- Content area -->
    </Border>
</Window>
```

## Critérios de Aceite
- [ ] Janela aparece sem borda, fundo transparente
- [ ] Sempre topmost
- [ ] Clicks passam para janela abaixo
- [ ] Não aparece na taskbar
- [ ] Não aparece no Alt+Tab
- [ ] Posição é configurável
- [ ] Visual: borda arredondada, fundo semi-transparente escuro

## Testes
- Manual: abrir overlay sobre app qualquer, clicar "através" dele
- Manual: Alt+Tab não mostra overlay
- Manual: arrastar overlay para diferentes posições
- Unit: window styles são aplicados corretamente
