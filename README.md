# Aimon

**Make your AIs your own.** Aimon is a desktop app that personalizes the AI experience —
visually (character-driven animations), stylistically (per-task personalities and interaction
rules), and practically (use any combination of commercial AI models) — through portable,
clearly structured configuration profiles.

Built with **.NET 10** and **Avalonia**.

## Solution layout

Clean Architecture; dependencies point inward only:

```
src/
  Aimon.Domain          # entities, value objects, rules — no dependencies
  Aimon.Application     # use cases + ports (interfaces)     -> Domain
  Aimon.Infrastructure  # adapters (providers, storage, …)   -> Application, Domain
  Aimon.Desktop         # Avalonia UI + composition root      -> Application, Infrastructure
tests/
  Aimon.Domain.Tests
  Aimon.Application.Tests
  Aimon.Infrastructure.Tests
build/                  # build & test scripts
planning/               # project description + implementation plan
```

## Build & test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# From the repo root
dotnet build Aimon.slnx -c Release
dotnet test  Aimon.slnx -c Release

# Or use the helper scripts
pwsh build/build.ps1      # Windows / cross-platform PowerShell
./build/build.sh          # bash
```

Run the desktop app:

```bash
dotnet run --project src/Aimon.Desktop
```

## Documentation

- [`planning/implementation_plan.md`](planning/implementation_plan.md) — phased roadmap with
  per-milestone acceptance criteria.
- [`planning/Aimon.docx`](planning/Aimon.docx) — original project description.

## Contributing / workflow

See [CONTRIBUTING.md](CONTRIBUTING.md) for branch naming, commit conventions, and the pull-request
flow.
