# Zefa IA — Project Specification

## Vision

Real-time meeting assistant for Windows that captures audio (microphone + system loopback), transcribes speech live, and uses an LLM to generate contextual suggestions displayed in a non-intrusive overlay — invisible to screen sharing.

## Decisions

| Area | Choice | Notes |
|------|--------|-------|
| **Stack** | C# / WPF (.NET 8) | Native Windows APIs (WASAPI, overlay, display affinity) |
| **Target** | Personal use, solo | No auth, no backend, local-first |
| **Audio** | WASAPI loopback global + mic | Two separate streams for free diarization |
| **STT (MVP)** | Whisper local (whisper.net or faster-whisper via process) | Abstraction layer for provider swap |
| **STT (future)** | ElevenLabs Scribe v2 Realtime | WebSocket streaming, configurable via UI |
| **LLM** | Claude API with prompt caching | System prompt = profile + meeting context |
| **LLM trigger** | Automatic (silence ~1.5s) + hotkey manual | Both available simultaneously |
| **Overlay** | WPF topmost, click-through + mini controls | Copy, dismiss, pin. Excluded from capture |
| **Persistence** | SQLite local per meeting session | Transcription + suggestions. Deletable |
| **Languages** | Multilingual (PT-BR + EN minimum) | Auto-detect language |
| **Profile** | Static profile file + per-meeting context | Agenda/objective input before each meeting |
| **Critical NFR** | Latency < 2s end-to-end | Audio → STT → LLM → overlay in under 2 seconds |

## Architecture Overview

```
[Microphone] ──► [Audio Capture Engine] ──► [STT Provider] ──► [Transcription Engine] ──► [LLM Client] ──► [Overlay]
[Loopback]   ──►        (NAudio)        ──►  (ISTTProvider)  ──►   (diarized text)    ──►  (Claude)    ──►  (WPF)
                                                                                              ▲
                                                                                    [Profile + Context]
                                                                                              ▲
                                                                                    [SQLite Session Store]
```

## Key Interfaces

- `IAudioSource` — abstracts mic vs loopback capture
- `ISTTProvider` — abstracts Whisper vs ElevenLabs vs others
- `ILLMClient` — abstracts Claude vs future providers
- `ITriggerStrategy` — abstracts silence detection vs hotkey

## Sprint Map

| Sprint | Theme | Deliverable |
|--------|-------|-------------|
| 1 | Audio Capture | Dual-stream capture working, AEC, WAV export for verification |
| 2 | Speech-to-Text | Live transcription from audio streams, provider abstraction |
| 3 | Overlay UI | Click-through overlay with mini controls, excluded from capture |
| 4 | LLM Integration | Claude suggestions triggered by silence/hotkey, streaming render |
| 5 | Persistence & Config | SQLite sessions, profile editor, meeting context, settings UI |
| 6 | Integration & Polish | End-to-end flow, system tray, installer, performance tuning |

## File Structure (Target)

```
ZefaIA/
├── ZefaIA.sln
├── src/
│   ├── ZefaIA.Core/           # Domain models, interfaces, events
│   ├── ZefaIA.Audio/          # NAudio capture, AEC, audio pipeline
│   ├── ZefaIA.STT/            # STT providers (Whisper, ElevenLabs)
│   ├── ZefaIA.LLM/            # LLM client (Claude API)
│   ├── ZefaIA.Overlay/        # WPF overlay window
│   ├── ZefaIA.Persistence/    # SQLite storage
│   └── ZefaIA.App/            # Main WPF application, DI, config
├── tests/
│   ├── ZefaIA.Audio.Tests/
│   ├── ZefaIA.STT.Tests/
│   ├── ZefaIA.LLM.Tests/
│   └── ZefaIA.Integration.Tests/
└── docs/
```

## Ownership

- **Author & sole contributor:** Aennson (aennson@gmail.com)
- Commits must NOT include `Co-Authored-By` from Claude or any AI
- All authorship belongs exclusively to the project owner

## Constraints

- Windows 10 1903+ (WASAPI loopback)
- .NET 8
- No cloud backend — all processing local except STT API calls (when using ElevenLabs) and LLM API calls
- LGPD: default retention is per-session local; user explicitly chooses to save
