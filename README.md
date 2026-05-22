# Kenaz

A private, local-first daily wellbeing check-in: log mood, energy, and sleep, and see your patterns over time. *Bring it into the light.*

C# solution with a domain core, a console front-end, and an NUnit test project — growing along the Emne 3 curriculum toward a SQLite store, an ASP.NET Minimal API, and a mobile-first PWA.

## Projects

- `Kenaz.Core` — domain model and rules. No `Console`, no IO.
- `Kenaz.Console` — console front-end; calls into `Kenaz.Core`.
- `Kenaz.Tests` — NUnit; references `Kenaz.Core` only.

## Run

```powershell
dotnet build Kenaz.slnx
dotnet test Kenaz.slnx
dotnet run --project Kenaz.Console
```

Design spec: `docs/superpowers/specs/2026-05-21-kenaz-design.md`
