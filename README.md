# Kenaz

A private, local-first daily wellbeing check-in: log mood, energy, and sleep, and see your patterns over time. *Bring it into the light.*

C# solution with a domain core, a console front-end, and an NUnit test project — growing toward a SQLite store, an ASP.NET Minimal API, and a mobile-first PWA.

## Projects

- `Kenaz.Core` — domain model, rules, and insights. No `Console`; file IO is isolated to the JSON storage adapter behind a repository interface.
- `Kenaz.Console` — console front-end; calls into `Kenaz.Core`.
- `Kenaz.Tests` — NUnit; references `Kenaz.Core` only.

## Run

```powershell
dotnet build Kenaz.slnx
dotnet test Kenaz.slnx
dotnet run --project Kenaz.Console
```

In the app you can check in for today (mood, energy, sleep, and a note — each optional), see today against your last 7 days with a gentle streak, browse your history, and export or import your check-ins.

## Data

Check-ins are stored locally as JSON in `%APPDATA%\Kenaz\checkins.json`. Nothing leaves your machine.

Export saves all your check-ins to `Documents\Kenaz\kenaz-backup-<timestamp>.json`; import merges a backup back in, where the more recently edited entry wins so a restore never overwrites newer changes. The export file is plain, unencrypted JSON — keep it somewhere private.

Design spec: `docs/superpowers/specs/2026-05-21-kenaz-design.md`

## License

Apache License 2.0 — see [LICENSE](LICENSE).
