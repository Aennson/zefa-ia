# Guia de Uso — Zefa IA

Assistente de reunioes em tempo real. Escuta o audio da reuniao, transcreve ao
vivo e sugere pontos contextuais em um overlay que nao aparece quando voce
compartilha a tela.

---

## 1. Instalacao

1. Baixe `ZefaIA-Setup-<versao>.exe`
2. Execute o instalador (nao precisa de administrador)
3. Opcionalmente marque "Iniciar o Zefa IA junto com o Windows"

O app nao tem janela principal — depois de instalado ele vive no **system tray**,
ao lado do relogio.

### Requisitos

- Windows 10 versao 1903 ou superior (necessario para captura de audio do sistema)
- Nenhuma instalacao de .NET — o instalador ja inclui tudo
- **Microsoft Visual C++ 2015-2022 Redistributable (x64)** — o motor de transcricao
  local usa bibliotecas nativas que nao carregam sem ele. O instalador avisa se
  estiver faltando. Para instalar:

  ```powershell
  winget install Microsoft.VCRedist.2015+.x64
  ```

  Ou baixe em https://aka.ms/vs/17/release/vc_redist.x64.exe

---

## 2. Primeira configuracao

Clique com o botao direito no icone da bandeja → **Configuracoes**.

### Perfil (o que mais melhora as sugestoes)

| Campo | Para que serve |
|-------|----------------|
| Nome | Como a Zefa se refere a voce |
| Cargo / Funcao | Ajusta o angulo das sugestoes (tecnico vs comercial) |
| Area de expertise | Evita sugerir o obvio no que voce ja domina |
| Tom preferido | Formal, Casual ou Tecnico |
| Contexto adicional | Qualquer coisa recorrente: empresa, produto, time |

Preencher o perfil e o passo com maior impacto na qualidade das sugestoes.
Sem ele a Zefa gera conselhos genericos.

### Chave da API do Claude

As sugestoes usam a API do Claude. Defina a variavel de ambiente:

```powershell
# Permanente, para o seu usuario
[Environment]::SetEnvironmentVariable("ANTHROPIC_API_KEY", "sua-chave-aqui", "User")
```

Feche e reabra o Zefa IA depois de definir.

> **Sem a chave o app continua funcionando** — transcreve, salva no historico e
> exporta normalmente. Apenas as sugestoes ficam desligadas.

Chave em https://platform.claude.com/ → **API keys**.

> Assinatura do Claude.ai (Pro/Max) **nao da acesso a API** — sao cobrados
> separadamente. A API usa creditos pre-pagos, adicionados em *Billing*.

---

## 3. Usando em uma reuniao

### Iniciar

Botao direito na bandeja → **Nova Reuniao** (ou duplo-clique no icone).

Preencha o que fizer sentido:

- **Titulo** — se deixar vazio, vira `Reuniao <data> <hora>`
- **Template** — 1:1, Standup, Review ou Custom preenchem agenda e objetivo
- **Agenda / Objetivo / Participantes** — vao para o prompt do LLM

Com pressa? **Inicio Rapido** pula tudo isso e comeca a gravar na hora.

### Durante

O overlay aparece com a transcricao ao vivo, em duas abas: **Transcricao** e
**Sugestoes**. Ele salta para Sugestoes quando uma chega — clique em
**Transcricao** para voltar.

Voce pode arrastar o overlay pelo cabecalho e **redimensionar por qualquer borda
ou canto** (a alca fica no canto inferior direito). O tamanho vale enquanto o app
estiver aberto; ao reiniciar ele volta ao padrao.

| Atalho | Acao | Status |
|--------|------|--------|
| `Ctrl+Shift+Space` | Pedir sugestao agora | Funciona |
| `Ctrl+Shift+Z` | Mostrar/ocultar overlay | **Nao implementado** |
| `Ctrl+Shift+C` | Copiar ultima sugestao | **Nao implementado** |

Os dois ultimos aparecem nas Configuracoes mas ainda nao sao registrados no
Windows. Use o botao de copiar no proprio overlay enquanto isso.

> **Sugestao automatica por silencio:** a Zefa tambem sugere sozinha apos ~1,5s
> de silencio — mas o gatilho observa o **audio que sai pelas caixas**, nao o seu
> microfone. Em uma reuniao de verdade isso funciona (a voz do outro entra por
> ali). Testando sozinho, com o PC mudo, nenhuma sugestao automatica acontece:
> use `Ctrl+Shift+Space`.

O icone da bandeja fica **vermelho** enquanto grava.

### Encerrar

Botao direito → **Parar Reuniao**. A transcricao e as sugestoes sao salvas
automaticamente.

---

## 4. O overlay e o compartilhamento de tela

O overlay usa `SetWindowDisplayAffinity` com `WDA_EXCLUDEFROMCAPTURE`: ele fica
visivel para voce, mas **some das gravacoes e do compartilhamento de tela** no
Teams, Meet, Zoom e OBS.

Isso vale para captura por software. Uma camera apontada para o monitor
obviamente enxerga tudo.

Se preferir desligar, desmarque "Ocultar de captura de tela" nas Configuracoes.

---

## 5. Historico

Botao direito na bandeja → **Historico**.

- Lista de reunioes por data, com duracao
- Clique em uma para ver transcricao e sugestoes intercaladas na ordem em que aconteceram
- Campo de busca procura no titulo, na agenda **e dentro da transcricao**
- **Exportar TXT** — ata legivel, com sugestoes inline
- **Exportar JSON** — dados estruturados, para processar em outro lugar
- **Deletar** — remove a reuniao e tudo associado, com confirmacao

---

## 6. Escolhendo o provedor de transcricao

| | Whisper Local | ElevenLabs |
|---|---|---|
| Custo | Gratis | Pago por uso |
| Privacidade | 100% offline | Audio vai para a nuvem |
| Precisao | Boa (melhora com modelo maior) | Muito boa |
| Latencia | Depende da CPU/GPU | Baixa e constante |
| Precisa de internet | Nao | Sim |

**Padrao: Whisper Local.** Escolha ElevenLabs so se a precisao do Whisper nao
estiver dando conta. Exige `ELEVENLABS_API_KEY`, definida do mesmo jeito que a
chave do Claude. A chave se gera em https://elevenlabs.io → perfil → **API Keys**.

> **ElevenLabs nao substitui o Claude.** Ela troca apenas quem transcreve o audio.
> As sugestoes continuam vindo do Claude, e sem `ANTHROPIC_API_KEY` continuam
> desligadas — configurar ElevenLabs sozinha nao faz a Zefa responder nada.

### Modelos do Whisper

| Modelo | Tamanho | Quando usar |
|--------|---------|-------------|
| tiny | ~75 MB | Maquina fraca, so quer o essencial |
| base | ~142 MB | **Padrao** — bom equilibrio |
| small | ~466 MB | Audio dificil, sotaques |
| medium | ~1.5 GB | Maxima precisao, precisa de GPU |

Baixado automaticamente no primeiro uso.

**Marque "Usar GPU (CUDA)"** se tiver placa NVIDIA — a diferenca de velocidade e
grande, principalmente em `small` e `medium`.

---

## 7. Seus dados

Tudo fica local:

```
%APPDATA%\ZefaIA\
├── meetings.db      historico de reunioes
├── settings.json    suas configuracoes
└── crashes\         relatorios de erro (nunca enviados)
```

Saem da sua maquina apenas:

- O texto da transcricao recente, enviado a API do Claude para gerar sugestoes
- O audio, se voce escolher ElevenLabs como provedor de STT

Com Whisper local e sem chave do Claude, **nada sai da maquina**.

Para apagar tudo: delete `%APPDATA%\ZefaIA`. Para apagar uma reuniao so, use o
botao Deletar no Historico.

---

## 8. Problemas comuns

### O overlay nao aparece

1. Confirme que a reuniao esta rodando — icone da bandeja vermelho
2. Se estiver em monitor secundario, mude a posicao nas Configuracoes
3. Duplo-clique no icone da bandeja alterna a exibicao

### "Nao foi possivel carregar o motor Whisper"

A mensagem diz qual dos dois casos e:

- **"a biblioteca nativa nao foi encontrada"** → problema de empacotamento: a
  pasta `runtimes` precisa estar junto do executavel
- **"o Windows recusou carrega-la"** → falta o Visual C++ Redistributable:
  `winget install Microsoft.VCRedist.2015+.x64`

### A primeira reuniao trava ao iniciar

Nao travou: esta baixando o modelo do Whisper (~142 MB) **sem barra de
progresso**. Aguarde alguns minutos. Nas proximas vezes e imediato.

### Nao transcreve nada

Provavel causa em ordem:

1. **Microfone errado** — o app usa o dispositivo padrao do Windows. Ajuste em
   Configuracoes do Windows → Sistema → Som
2. **Modelo ainda baixando** — a primeira execucao baixa ~142 MB; aguarde
3. **Permissao de microfone** — Windows → Privacidade → Microfone → permita
   apps de desktop

### So transcreve a minha voz (ou so a do outro)

O app captura duas fontes separadas: microfone (voce) e loopback do sistema (os
outros). Se falta um lado:

- **Falta "Interlocutor"** → o audio da reuniao nao esta saindo pelo dispositivo
  padrao. Fones USB e headsets bluetooth costumam trocar o dispositivo de saida
- **Falta "Eu"** → microfone mudo ou errado

### Nao aparecem sugestoes

1. **Aperte `Ctrl+Shift+Space`.** A sugestao automatica depende do audio que sai
   pelas caixas; testando sozinho com o PC mudo ela nunca dispara. Esse e de longe
   o motivo mais comum
2. `ANTHROPIC_API_KEY` esta definida **no nivel do usuario**? Uma variavel setada
   numa janela do PowerShell nao chega ao app aberto pelo Explorer. Reinicie o app
   depois de definir
3. Alguem falou algo? A Zefa nao sugere sem transcricao recente
4. Ela responde `[SEM SUGESTAO]` de proposito quando nao tem nada util a dizer —
   isso e comportamento normal, nao falha
5. Ha limite de 4 sugestoes por minuto para controlar custo

### O atalho `Ctrl+Shift+Space` nao faz nada

O Windows entrega uma combinacao global a quem registrou primeiro, e o registro
falha **em silencio**. Se outro app ja usa essa combinacao, feche-o para testar.
Trocar o atalho ainda nao esta implementado.

### Creditos e cobranca do Claude

Chave valida mas sem creditos retorna erro de billing na primeira sugestao.
Adicione creditos em https://platform.claude.com → **Billing**.

### O app esta pesado

- Troque para o modelo `tiny` do Whisper
- Ligue GPU se tiver NVIDIA
- `medium` sem GPU derruba a maioria das maquinas — evite

### Alguma coisa quebrou

Veja `%APPDATA%\ZefaIA\crashes\`. Os relatorios ja vem com chaves de API e nome
de usuario removidos, entao da para compartilhar sem risco.

---

## 9. Como funciona por dentro

```
[Microfone] ──► Captura ──► Cancelamento ──► Whisper/    ──► Transcricao
[Sistema]   ──►  dupla      de eco (NLMS)    ElevenLabs      diarizada
                                                                 │
                                    ┌────────────────────────────┤
                                    ▼                            ▼
                            Detecta pausa ──► Claude API ──► Overlay
                                                   │
                                                   ▼
                                            SQLite (historico)
```

**Diarizacao sem ML** — quem falou vem da origem do stream: microfone = voce,
loopback = interlocutor. Simples e nao erra.

**Cancelamento de eco** — o audio que sai pela caixa de som vaza de volta pelo
microfone. Um filtro adaptativo NLMS remove essa duplicacao.

**Prompt caching** — o perfil e o contexto da reuniao sao marcados como cache no
Claude, entao so a transcricao nova e cobrada como entrada nova.

Detalhes em [`docs/PROJECT-SPEC.md`](PROJECT-SPEC.md).
