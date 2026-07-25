# Testes: AudioDeviceEnumerator

**Arquivo fonte:** `src/ZefaIA.Audio/AudioDeviceEnumerator.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/AudioDeviceEnumeratorTests.cs`
**Classe de teste:** `AudioDeviceEnumeratorTests`

## Motivacao

`AudioDeviceEnumerator` lista dispositivos de audio disponivel no sistema via `MMDeviceEnumerator` (WASAPI). Todos os testes requerem hardware Windows e estao marcados com `Skip`.

## Testes

### 1. `GetMicrophones_ReturnsDeviceList` *(Skip: requer hardware)*
- **Tipo:** Hardware
- **O que testa:** Enumeracao de microfones retorna lista nao-nula
- **Como funciona:** Chama `GetMicrophones()`, verifica que o retorno nao e null.
- **Por que existe:** Valida que a API WASAPI `MMDeviceEnumerator` funciona e retorna dispositivos de captura. Necessario para a UI de selecao de dispositivo (Sprint 3).
- **Execucao:** Apenas Windows com dispositivos de audio

### 2. `GetOutputDevices_ReturnsDeviceList` *(Skip: requer hardware)*
- **Tipo:** Hardware
- **O que testa:** Enumeracao de dispositivos de saida retorna lista nao-nula
- **Como funciona:** Chama `GetOutputDevices()`, verifica que o retorno nao e null.
- **Por que existe:** Necessario para identificar o dispositivo de loopback. Se o enum falhar, o app nao consegue iniciar captura.
- **Execucao:** Apenas Windows com dispositivos de audio
