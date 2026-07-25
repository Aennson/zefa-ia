# Task 2-4: Diarização por Stream

## Descrição
Implementar identificação de speaker baseada na origem do stream de áudio. Mic = "Eu" (usuário), Loopback = "Interlocutor".

## Skills
- `simplify` — revisar modelo de dados

## Dependências
- Task 2-3 concluída (pipeline de transcrição rodando)

## Entregáveis
- `SpeakerLabel` model com nome configurável
- Mapeamento `AudioSourceType → SpeakerLabel`
- Formatação de transcrição com labels: `[Eu] Olá, tudo bem?` / `[Interlocutor] Tudo ótimo!`
- Timeline ordenada por timestamp combinando ambos streams
- Configuração de nomes de speaker

## Detalhes
- A diarização "grátis" vem da separação de streams — sem ML
- Desafio: quando mic e loopback captam a mesma fala (eco), AEC deve ter resolvido
- Overlap: se ambos falam ao mesmo tempo, ambos segmentos aparecem com timestamps sobrepostos
- Para o futuro: se houver múltiplos interlocutores no loopback, todos aparecem como "Interlocutor" (limitação aceita no MVP)

## Critérios de Aceite
- [ ] Segmentos de mic são labelados como "Eu"
- [ ] Segmentos de loopback são labelados como "Interlocutor"
- [ ] Timeline é ordenada cronologicamente
- [ ] Nomes são configuráveis
- [ ] Sobreposições são tratadas sem crash

## Testes
- Unit: mapeamento source → label funciona
- Unit: timeline merge ordena corretamente com timestamps intercalados
- Unit: sobreposições são preservadas (não descartadas)
