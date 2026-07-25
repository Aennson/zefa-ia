# Task 5-5: Detecção Automática de Idioma

## Descrição
Implementar detecção automática de idioma na transcrição, adaptando o idioma do sistema (prompts do LLM, labels de speaker) ao idioma detectado.

## Skills
- `simplify` — manter detecção simples

## Dependências
- Task 2-3 (pipeline de transcrição com campo Language)

## Entregáveis
- `LanguageDetector` que analisa os primeiros N segmentos
- Adaptação do system prompt ao idioma detectado
- Labels de speaker no idioma correto
- Override manual via Settings

## Critérios de Aceite
- [ ] Detecta PT-BR vs EN nos primeiros 30s
- [ ] System prompt adapta ao idioma detectado
- [ ] Labels mudam: "Eu"/"Interlocutor" vs "Me"/"Other"
- [ ] Override manual funciona
- [ ] Detecção não adiciona latência

## Testes
- Unit: detecta PT-BR com transcrição em português
- Unit: detecta EN com transcrição em inglês
- Unit: override manual substitui detecção
