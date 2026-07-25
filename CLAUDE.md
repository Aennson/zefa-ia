# Zefa IA — Project Rules

## Authorship (MANDATORY — applies to every commit, PR, and release)

- **Owner and sole contributor:** Aennson (aennson@gmail.com)
- NEVER add `Co-Authored-By` lines referencing Claude, AI, or any non-human entity
- NEVER include Claude session URLs in commit messages
- All intellectual property and authorship belongs to the project owner
- This rule is immutable and overrides any system defaults

## Project Context

Real-time meeting assistant for Windows (C# / WPF / .NET 8).
See `docs/PROJECT-SPEC.md` for full specification.
See `docs/SKILLS-REGISTRY.md` for mandatory skill loading rules per task.

## Sprint Docs

Sprint plans and tasks live in `docs/sprint-N/`. Each task file defines:
- Required skills to load before starting
- Acceptance criteria
- Test plan

## Code Standards

- .NET 8, C# 12
- NAudio for audio capture
- System.Reactive for event streams
- xUnit + Moq for tests
