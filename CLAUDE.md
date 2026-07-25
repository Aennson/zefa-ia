# Zefa IA — Project Rules

## Authorship (MANDATORY — applies to every commit, PR, and release)

- **Owner:** Aennson (aennson@gmail.com)
- **Co-author:** Claude (AI assistant)
- Every commit MUST include the following co-authorship line:
  `Co-Authored-By: Claude <noreply@anthropic.com>`
- Aennson is the primary author; Claude is co-author

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
