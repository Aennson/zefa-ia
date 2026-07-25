# Task 5-1: Modelo de Dados e SQLite Setup

## Descrição
Definir o esquema do banco de dados SQLite para armazenar sessões de reunião, transcrições, e sugestões.

## Skills
- `simplify` — manter esquema mínimo
- `security-review` — dados sensíveis não devem ser expostos

## Dependências
- Task 1-1 (projeto ZefaIA.Persistence existe)

## Entregáveis
- Esquema SQLite com migrations
- `MeetingSession`, `TranscriptionEntry`, `SuggestionEntry` entities
- `IMeetingRepository` interface
- `SqliteMeetingRepository` implementação
- DB file em `%APPDATA%/ZefaIA/meetings.db`

## Esquema
```sql
CREATE TABLE MeetingSessions (
    Id TEXT PRIMARY KEY,          -- GUID
    Title TEXT,
    StartedAt TEXT NOT NULL,      -- ISO 8601
    EndedAt TEXT,
    MeetingContext TEXT,          -- agenda/objective JSON
    ProfileSnapshot TEXT,        -- perfil usado nesta reunião
    Language TEXT,
    DurationSeconds INTEGER
);

CREATE TABLE TranscriptionEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId TEXT NOT NULL REFERENCES MeetingSessions(Id) ON DELETE CASCADE,
    Speaker TEXT NOT NULL,        -- "Eu" / "Interlocutor"
    Text TEXT NOT NULL,
    Language TEXT,
    Confidence REAL,
    StartTime TEXT NOT NULL,      -- TimeSpan serializado
    EndTime TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE SuggestionEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId TEXT NOT NULL REFERENCES MeetingSessions(Id) ON DELETE CASCADE,
    TriggerType TEXT NOT NULL,    -- "Silence" / "Hotkey"
    TranscriptContext TEXT,       -- trecho de transcrição que gerou
    SuggestionText TEXT NOT NULL,
    TokensUsed INTEGER,
    CacheHit INTEGER,            -- boolean
    LatencyMs INTEGER,
    CreatedAt TEXT NOT NULL
);

CREATE INDEX idx_transcription_session ON TranscriptionEntries(SessionId);
CREATE INDEX idx_suggestion_session ON SuggestionEntries(SessionId);
```

## Critérios de Aceite
- [ ] DB é criado automaticamente na primeira execução
- [ ] Migrations rodam sem erro
- [ ] CRUD funciona para todas as entidades
- [ ] CASCADE delete funciona (deletar sessão deleta entries)
- [ ] DB path é `%APPDATA%/ZefaIA/meetings.db`
- [ ] Sem dados sensíveis logados

## Testes
- Unit: CRUD para MeetingSession
- Unit: CRUD para TranscriptionEntry
- Unit: CRUD para SuggestionEntry
- Unit: cascade delete funciona
- Unit: migration cria schema corretamente em DB nova
