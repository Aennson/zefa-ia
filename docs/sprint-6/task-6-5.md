# Task 6-5: Installer/Packaging

## Descrição
Criar instalador para distribuição do Zefa IA, com todas as dependências incluídas.

## Skills
- `simplify` — manter packaging direto

## Dependências
- Task 6-4 (app otimizado e estável)

## Entregáveis
- Build de release (self-contained, single-file se possível)
- Installer (.exe via Inno Setup ou .msix)
- Auto-start com Windows (opcional, configurável)
- Desinstalador limpo
- Inclusão do modelo Whisper no installer (ou download no primeiro uso)

## Opções de Packaging
1. **Self-contained single-file** — `dotnet publish -c Release --self-contained -p:PublishSingleFile=true`
   - Pro: sem dependência de .NET runtime
   - Con: arquivo grande (~80MB + modelo Whisper)
2. **MSIX** — Windows Store compatible
   - Pro: auto-update, sandbox
   - Con: mais complexo de configurar
3. **Inno Setup** — installer clássico
   - Pro: flexível, conhecido
   - Con: sem auto-update nativo

## Critérios de Aceite
- [ ] Installer funciona em máquina limpa (sem .NET instalado)
- [ ] App inicia após instalação
- [ ] Desinstalador remove tudo (exceto dados do usuário, se escolher)
- [ ] Modelo Whisper é incluído ou baixado automaticamente
- [ ] Atalho no Start Menu é criado
- [ ] Auto-start é configurável

## Testes
- Manual: instalar em VM limpa — app funciona
- Manual: desinstalar — verificar que foi removido
- Manual: auto-start funciona após reboot
