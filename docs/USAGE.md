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

Chave em https://console.anthropic.com/

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

O overlay aparece com a transcricao ao vivo. A Zefa sugere sozinha quando
detecta uma pausa de ~1,5s na fala — o momento em que voce provavelmente vai
responder.

| Atalho | Acao |
|--------|------|
| `Ctrl+Shift+Space` | Pedir sugestao agora |
| `Ctrl+Shift+Z` | Mostrar/ocultar overlay |
| `Ctrl+Shift+C` | Copiar ultima sugestao |

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
estiver dando conta — exige `ELEVENLABS_API_KEY` definida como a chave do Claude.

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
2. Aperte `Ctrl+Shift+Z` (pode ter sido ocultado sem querer)
3. Se estiver em monitor secundario, mude a posicao nas Configuracoes

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

1. `ANTHROPIC_API_KEY` esta definida? Reinicie o app depois de definir
2. Alguem falou algo? A Zefa nao sugere sem transcricao recente
3. Ela responde `[SEM SUGESTAO]` de proposito quando nao tem nada util a dizer —
   isso e comportamento normal, nao falha
4. Ha limite de 4 sugestoes por minuto para controlar custo

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
