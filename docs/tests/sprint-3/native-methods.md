# Testes: NativeMethods

**Arquivo fonte:** `src/ZefaIA.Overlay/NativeMethods.cs`
**Arquivo de teste:** `tests/ZefaIA.Overlay.Tests/NativeMethodsTests.cs`
**Classe de teste:** `NativeMethodsTests`

## Motivacao

`NativeMethods` contem as constantes Win32 e P/Invoke para click-through, display affinity e hit-testing. As constantes devem ter os valores exatos da documentacao do Windows SDK — um valor errado torna o overlay visivel no screen sharing ou nao-click-through.

## Testes

### 1. `WindowStyle_Constants_HaveCorrectValues`
- **Tipo:** Unit
- **O que testa:** WS_EX_TRANSPARENT, WS_EX_LAYERED, WS_EX_TOOLWINDOW, WS_EX_NOACTIVATE
- **Como funciona:** Compara com valores da MSDN.
- **Por que existe:** Constantes erradas fariam o overlay aparecer na taskbar ou nao ser click-through.
- **Execucao:** `dotnet test --filter "NativeMethodsTests.WindowStyle_Constants_HaveCorrectValues"`

### 2. `DisplayAffinity_Constants_HaveCorrectValues`
- **Tipo:** Unit
- **O que testa:** WDA_NONE, WDA_MONITOR, WDA_EXCLUDEFROMCAPTURE
- **Como funciona:** Compara com valores documentados da API.
- **Por que existe:** WDA_EXCLUDEFROMCAPTURE errado = overlay visivel no screen sharing.
- **Execucao:** `dotnet test --filter "NativeMethodsTests.DisplayAffinity_Constants_HaveCorrectValues"`

### 3. `HitTest_Constants_HaveCorrectValues`
- **Tipo:** Unit
- **O que testa:** WM_NCHITTEST, HTTRANSPARENT, HTCLIENT, HTCAPTION
- **Execucao:** `dotnet test --filter "NativeMethodsTests.HitTest_Constants_HaveCorrectValues"`

### 4. `GWL_EXSTYLE_IsCorrect`
- **Tipo:** Unit
- **O que testa:** Constante GWL_EXSTYLE = -20
- **Execucao:** `dotnet test --filter "NativeMethodsTests.GWL_EXSTYLE_IsCorrect"`
