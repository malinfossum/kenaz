# Kenaz

A private, local-first daily wellbeing check-in: log mood, energy, and sleep, and see your patterns over time. *Bring it into the light.*

C# solution with a domain core, a console front-end, a loopback HTTP API, and an NUnit test project — built around a SQLite store, with a mobile-first PWA on the roadmap.

## The name

**Kenaz** (pronounced *KEN-ahz*) is the Elder Futhark rune for *torch* — *to spark, to bring into the light.* It's from the runic alphabet of the early Norse and Germanic peoples, the same lineage the Vikings later carved into weapons, monuments, and amulets. In Norwegian: *å tenne, å bringe frem i lyset.*

Kenaz is the fire-family sibling to [Ignite](https://github.com/malinfossum/ignite), my local-first ADHD task PWA: where Ignite is *a small flame, kept going*, Kenaz is the torch you hold up to see your week clearly. Hence the tagline.

To me, Kenaz is about consistency, reflection, and the self-care that lets you become a better version of yourself and put your energy where it counts. Coming from social work — and living with ADHD — I've learned you can't pour from an empty cup: put on your own oxygen mask first, then help the person next to you. The Norwegian words I live by — *egensikkerhet*, *egenomsorg*, *ta vare på deg selv*, *bruk energi på det som betyr noe og som gir noe tilbake* — are the values this tool is built around.

## Projects

- `Kenaz.Core` — domain model, rules, and insights. No `Console`; file IO is isolated to the storage adapters behind a repository interface.
- `Kenaz.Console` — console front-end; calls into `Kenaz.Core`.
- `Kenaz.Api` — loopback HTTP API over the same check-ins (M5); groundwork for the upcoming PWA, not part of daily use yet.
- `Kenaz.Tests` — NUnit; references `Kenaz.Core` and `Kenaz.Api`.

## Run

```powershell
dotnet build Kenaz.slnx
dotnet test Kenaz.slnx
dotnet run --project Kenaz.Console
```

In the app you can check in for today (mood, energy, sleep, and a note — each optional), see today against your last 7 days with a gentle streak, open a weekly review (brightest and hardest day, plus a small sleep–mood pattern when there's enough data), browse your history, and export or import your check-ins.

## Local API (M5)

Kenaz includes an optional loopback HTTP API over the same check-ins — groundwork for an upcoming mobile-first PWA, not part of daily use yet.

```powershell
dotnet run --project Kenaz.Api
```

On startup it prints the local URL and a bearer token, e.g. `Kenaz API → http://127.0.0.1:5247  (Authorization: Bearer …)`. The API binds to loopback only (`127.0.0.1` / `[::1]`) — it is never reachable from another machine — and every request needs that token. The token is generated once and stored at `%APPDATA%\Kenaz\api-token`; treat it like a password for localhost.

Endpoints (all requiring `Authorization: Bearer <token>`, where `{date}` is `yyyy-MM-dd`):

| Method | Route | Does |
|---|---|---|
| `GET` | `/checkins` | List all check-ins, newest first |
| `GET` | `/checkins/{date}` | Read one day (`404` if absent) |
| `PUT` | `/checkins/{date}` | Create or update a day |
| `DELETE` | `/checkins/{date}` | Remove a day (`404` if absent) |
| `GET` | `/insights` | Computed insights: 7-day averages, streak, highlights, sleep–mood pattern (read-only) |

## Data

Check-ins are stored locally as a SQLite database in `%APPDATA%\Kenaz\checkins.db`. Nothing leaves your machine.

Export saves all your check-ins to `Documents\Kenaz\kenaz-backup-<timestamp>.json`; import merges a backup back in, where the more recently edited entry wins so a restore never overwrites newer changes. The export file is plain, unencrypted JSON — keep it somewhere private.

### Files you may see in `%APPDATA%\Kenaz\`

- `checkins.db` — the live store.
- `api-token` — the bearer token for the local API (M5), generated on first API run. Plaintext (same single-user caveat as your data); delete it to roll the token (a new one is generated next run).
- `checkins.backup-YYYYMMDD-HHMMSS.json` — written by the JSON → SQLite migration, in the same format as a normal export. You may occasionally see more than one if a previous migration was interrupted; they contain the same historic data (the timestamp in the filename tells you which is which), and any of them is importable via menu option 5 if you ever need to restore. Plaintext (same caveat as exports); safe to delete once you've confirmed your check-ins are intact in the new store (option 3 or 6).
- `checkins.json.corrupt-YYYYMMDD-HHMMSS.bak` / `checkins.db.corrupt-YYYYMMDD-HHMMSS.bak` — Kenaz sets the bad file aside (with this name) if it can't read it on startup, then starts with a fresh empty store. If you see one, the matching live file (`checkins.json` or `checkins.db`) was unreadable; the `.bak` is your last-known-good copy.

Design spec: `docs/superpowers/specs/2026-05-21-kenaz-design.md`

## License

Apache License 2.0 — see [LICENSE](LICENSE).
