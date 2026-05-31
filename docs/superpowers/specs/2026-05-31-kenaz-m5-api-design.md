# Kenaz M5 — Loopback HTTP API over the Data

A transport layer, not a feature. A new ASP.NET Core Minimal API exposes check-in CRUD over the existing SQLite store — bound to loopback, guarded by a bearer token. The journal, the console, the storage format, and the insights are unchanged. The only domain addition is a `Delete` the API needs; the only storage tweak is a one-line `busy_timeout` so overlapping requests wait instead of erroring.

---

## Context

M1 shipped the daily tool. M2 formalized the repository seam and added portable export/import. M3 layered insight functions on the journal. M4 swapped the live store to SQLite behind that same seam. Through all four, `WellbeingJournal` has stayed the one place that owns check-in behaviour — and the console has been its only caller.

M5 is the API promised by the original design ([2026-05-21-kenaz-design.md](2026-05-21-kenaz-design.md), milestone table line 84): *"ASP.NET Minimal API over the data (loopback-only)."* It is governed by the security stance on line 69: *"When the API arrives it stays loopback/token-guarded — never a public multi-user service."* It is the first half of the *carry-it-everywhere* step (lines 84–89); M6's PWA is the client that will consume this contract.

This is also where M4's one deferral comes due. The M4 spec's out-of-scope note ([2026-05-27-kenaz-m4-sqlite-design.md](2026-05-27-kenaz-m4-sqlite-design.md), line 249) said: *"No `Upsert(checkIn)` / `Delete(date)` methods on `ICheckInRepository`. M5 (API) is a more natural moment for that, with concrete needs in hand."* With the concrete need now in hand, the answer is smaller than anticipated: the journal already does upsert (`AddOrUpdate`) on top of `LoadAll`/`SaveAll`, and a `Delete` built the same way needs **no interface change** at all. The seam stays exactly as M2 drew it.

**Intended outcome:** `dotnet run --project Kenaz.Api` prints a loopback URL and a bearer token. With that token, the four endpoints read and write the same check-ins the console shows. Without it, every request is a 401. The console keeps working untouched; nothing about the user's data moves or changes shape.

---

## Concept

One new project (`Kenaz.Api`) — a thin transport adapter that maps HTTP to `WellbeingJournal` calls and holds **no domain logic of its own**. One new domain method (`WellbeingJournal.Delete`). One small storage tweak (a `busy_timeout` PRAGMA). The console, the export format, the insight functions, and the repository interface are not touched.

The same `IConfiguration` seam that lets the integration tests point at a throwaway database also lets manual verification do so — sidestepping the "the app is hardwired to `%APPDATA%`" problem that made M4's manual testing awkward.

---

## Architecture

```
Kenaz.Api/                          (NEW project — Microsoft.NET.Sdk.Web, net10.0)
  Program.cs                        composition root: config, Kestrel loopback bind, DI, auth, route mapping, startup banner
  Endpoints/
    CheckInEndpoints.cs             the four routes (extension method over a route group)
  Contracts/
    UpsertCheckInRequest.cs         PUT body  { Mood, Energy, Sleep, Note }
    CheckInResponse.cs              wire shape { Date, Mood, Energy, Sleep, Note, CreatedAt, UpdatedAt } + ToResponse mapper
  Auth/
    TokenStore.cs                   read-or-generate the persisted bearer token
    BearerTokenFilter.cs            IEndpointFilter: constant-time compare; 401 on miss

Kenaz.Core/Services/WellbeingJournal.cs   (+ bool Delete(DateOnly) — built on the existing LoadAll/SaveAll seam)
Kenaz.Core/Storage/SqliteCheckInRepository.cs   (+ one-line PRAGMA busy_timeout on connection open)

Kenaz.slnx                          (+ Kenaz.Api project, 4th in the solution)
```

**MVC framing holds.** `Kenaz.Core` is the Model; the console and the API are two independent transport adapters (View+Controller) over it. The API owns its wire contract (its DTOs) and maps HTTP ↔ journal; it adds no logic the console couldn't already reach. The only Core changes are `WellbeingJournal.Delete` (a `Services/` domain method) and the `busy_timeout` PRAGMA (inside `Storage/`), so the M3/M4 separation grep — `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'` — still returns nothing.

---

## New project (Kenaz.Api)

### `Program.cs` — composition root

```csharp
var builder = WebApplication.CreateBuilder(args);

// Config seam (defaults to the real locations; overridable by tests and manual runs).
var dbPath    = builder.Configuration["Kenaz:DbPath"]    ?? SqliteCheckInRepository.DefaultFilePath();
var tokenPath = builder.Configuration["Kenaz:TokenPath"] ?? TokenStore.DefaultTokenPath();
var port      = int.TryParse(builder.Configuration["Kenaz:Port"], out var p) ? p : 5247;
// Test override: a known token skips the file entirely.
var token     = builder.Configuration["Kenaz:Token"] ?? TokenStore.GetOrCreate(tokenPath);

builder.WebHost.ConfigureKestrel(k => k.ListenLocalhost(port));   // 127.0.0.1 + [::1] only

builder.Services.AddSingleton<ICheckInRepository>(_ => new SqliteCheckInRepository(dbPath));
builder.Services.AddSingleton(sp => new WellbeingJournal(sp.GetRequiredService<ICheckInRepository>(), () => DateTimeOffset.Now));
builder.Services.AddSingleton(new ApiToken(token));   // wraps the token for the filter

var app = builder.Build();

app.MapGroup("/checkins")
   .AddEndpointFilter<BearerTokenFilter>()
   .MapCheckInEndpoints();

app.Logger.LogInformation("Kenaz API → http://127.0.0.1:{Port}  (Authorization: Bearer {Token})", port, token);
app.Run();

public partial class Program;   // so WebApplicationFactory<Program> can host it in tests
```

`ListenLocalhost` binds only the loopback interfaces — the API is never reachable from another machine. The repository and journal are singletons (both effectively stateless — the repository opens a fresh connection per call; the journal holds only the repo and the clock). Endpoint handlers receive `WellbeingJournal` by DI.

### Endpoints (`CheckInEndpoints.cs`)

`{date}` is always `yyyy-MM-dd`, parsed with `DateOnly.TryParseExact(..., CultureInfo.InvariantCulture)` — locale-safe, consistent with the SQLite layer. A malformed date is a 400 before the journal is touched.

| Method | Route | Maps to | Success | Errors |
|---|---|---|---|---|
| `GET` | `/checkins` | `journal.History()` (newest first) | `200` `CheckInResponse[]` | — |
| `GET` | `/checkins/{date}` | `journal.GetByDate(date)` | `200` `CheckInResponse` | `404` if absent · `400` bad date |
| `PUT` | `/checkins/{date}` | `journal.AddOrUpdate(date, body…)` | `200` `CheckInResponse` | `400` bad date / invalid body |
| `DELETE` | `/checkins/{date}` | `journal.Delete(date)` *(new)* | `204` | `404` if absent · `400` bad date |

All four sit behind the group's bearer-token filter, so a request without a valid token is a `401` *before* routing — the per-route errors above are what an already-authorized caller can hit.

```csharp
public static RouteGroupBuilder MapCheckInEndpoints(this RouteGroupBuilder group)
{
    group.MapGet("/", (WellbeingJournal journal) =>
        Results.Ok(journal.History().Select(CheckInResponse.From)));

    group.MapGet("/{date}", (string date, WellbeingJournal journal) =>
        TryDate(date, out var d)
            ? journal.GetByDate(d) is { } c ? Results.Ok(CheckInResponse.From(c)) : Results.NotFound()
            : Results.BadRequest("Date must be yyyy-MM-dd."));

    group.MapPut("/{date}", (string date, UpsertCheckInRequest body, WellbeingJournal journal) =>
    {
        if (!TryDate(date, out var d)) return Results.BadRequest("Date must be yyyy-MM-dd.");
        try { return Results.Ok(CheckInResponse.From(journal.AddOrUpdate(d, body.Mood, body.Energy, body.Sleep, body.Note))); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
    });

    group.MapDelete("/{date}", (string date, WellbeingJournal journal) =>
        TryDate(date, out var d)
            ? journal.Delete(d) ? Results.NoContent() : Results.NotFound()
            : Results.BadRequest("Date must be yyyy-MM-dd."));

    return group;
}
```

`PUT` is an upsert by design — the date in the route is the resource key, so re-PUTting a date updates it. `AddOrUpdate`'s own validation (via `CheckIn`'s constructor) is the single source of truth for what a valid check-in is; the API just translates the `ArgumentException` it already throws — for an all-null body or an out-of-range scale — into a `400`. No validation logic is duplicated in the API.

### Contracts (DTOs, in `Kenaz.Api`)

```csharp
public record UpsertCheckInRequest(int? Mood, int? Energy, decimal? Sleep, string? Note);

public record CheckInResponse(string Date, int? Mood, int? Energy, decimal? Sleep,
                              string? Note, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static CheckInResponse From(CheckIn c) => new(
        c.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        c.Mood, c.Energy, c.Sleep, c.Note, c.CreatedAt, c.UpdatedAt);
}
```

The DTOs live in the API, not Core — the wire contract is the API's concern, and `CheckIn`/`CheckInDto` stay where they are. `Date` is serialized as the `yyyy-MM-dd` string the route uses (a stable, unambiguous key); System.Text.Json (ASP.NET's default) handles the rest — `decimal` as a JSON number, `DateTimeOffset` as ISO 8601.

### Auth (`TokenStore`, `BearerTokenFilter`)

```csharp
public static class TokenStore
{
    public static string DefaultTokenPath();                 // %APPDATA%\Kenaz\api-token
    public static string GetOrCreate(string path);           // read trimmed, or generate+persist
}
```

`GetOrCreate`: if the file exists, return its trimmed contents; otherwise generate 32 bytes from `RandomNumberGenerator`, Base64Url-encode them, create the directory if needed, write the file, and return the token. The token is persisted so it is stable across runs (M6 and curl sessions can rely on one value). Token-at-rest adds no exposure beyond the already-accepted plaintext database in the same folder (invariant #9).

`BearerTokenFilter : IEndpointFilter`, applied to the whole `/checkins` group: pull the `Authorization` header, require the `Bearer ` prefix, and compare the rest to the configured token with `CryptographicOperations.FixedTimeEquals` over the UTF-8 bytes (constant-time — no early-exit timing signal). Missing header, wrong scheme, or mismatch → `Results.Unauthorized()` (`401`) with a `WWW-Authenticate: Bearer` header. Because the filter sits on the group, all four endpoints are guarded by one line of wiring. The token is wrapped once in a small `ApiToken` record (defined alongside the filter in `BearerTokenFilter.cs`) and registered as a singleton, so the filter receives it by DI rather than capturing a bare string.

---

## New domain (Core)

### `WellbeingJournal.Delete`

```csharp
/// <summary>
/// Removes the check-in for <paramref name="date"/> if present. Returns true when a row
/// was removed, false when there was nothing to remove. Built on the same LoadAll/SaveAll
/// seam as AddOrUpdate — no repository change needed.
/// </summary>
public bool Delete(DateOnly date)
{
    var checkIns = _repository.LoadAll().ToList();
    var removed = checkIns.RemoveAll(c => c.Date == date) > 0;
    if (removed) _repository.SaveAll(checkIns);
    return removed;
}
```

Mirrors `AddOrUpdate`: load the list, mutate, save the list. Rewrite-on-delete rewrites the whole table, which at the spec's max of ~3650 rows is free ([M4 spec line 170](2026-05-27-kenaz-m4-sqlite-design.md): *"table scans are nothing"*). `SaveAll` skips the write when nothing was removed, so a `DELETE` of an absent date does no I/O and the API returns `404`.

### `SqliteCheckInRepository` — `busy_timeout`

The API can receive overlapping requests, which the M4 spec named as the moment to revisit concurrency ([line 171](2026-05-27-kenaz-m4-sqlite-design.md): *"revisit if M5's API ever introduces concurrency"*). The light touch: every connection the repository opens runs `PRAGMA busy_timeout = 3000;` immediately after `Open()`, via one shared helper the three existing call sites (schema, `LoadAllCore`, `SaveAll`) route through:

```csharp
private SqliteConnection OpenConnection()
{
    var conn = new SqliteConnection(_connectionString);
    conn.Open();
    using (var pragma = conn.CreateCommand()) { pragma.CommandText = "PRAGMA busy_timeout = 3000;"; pragma.ExecuteNonQuery(); }
    return conn;
}
```

With the default rollback journal, a second writer that arrives mid-transaction now **waits up to 3 s** for the lock instead of immediately throwing `SQLITE_BUSY` (a 500). For a single human clicking around that ceiling is never reached; it just turns a rare double-submit race into a brief wait. WAL stays out (YAGNI) — revisit only if read-during-write stalls ever show up. This is the single change inside `Storage/`, and it's a refactor that leaves behaviour identical for the console.

---

## Hosting & security

- **Loopback bind.** `ListenLocalhost(port)` → `127.0.0.1` and `[::1]` only. Remote machines cannot reach the API at all; the token guards against *local* callers (other processes, or a web page in the user's own browser fetching `localhost`).
- **HTTP, not HTTPS.** Loopback traffic never leaves the machine, so a TLS dev-certificate would be friction with no privacy gain. (If a future milestone ever exposes the API beyond loopback — explicitly out of scope and against the spec — TLS becomes mandatory.)
- **Token guard.** Persisted bearer token; constant-time compare; `401` on any failure. The token is never written to logs, and no endpoint logs request bodies (wellbeing content stays out of logs).
- **CSRF property (free).** State-changing requests require a non-simple `Authorization` header, which forces a CORS preflight. The API answers no CORS headers, so a browser blocks any cross-origin `PUT`/`DELETE` a malicious page tries to forge — the token requirement doubles as CSRF protection. CORS for M6's PWA origin is a deliberate M6 decision, made when that origin exists.
- **Untrusted-input boundary (invariant #6).** Every `PUT` body is validated by `CheckIn`'s constructor before it can persist; malformed input is a `400`, never a stored bad row. (Output escaping is an M6 render-boundary concern — there is no HTML here.)
- **Plaintext-at-rest (invariant #9).** Unchanged and still accepted through M5: the database, exports, and now the token file are plaintext in a single-user local folder.

---

## Decisions made (change either if you disagree)

| Decision | Choice | Why |
|---|---|---|
| Web style | Minimal API (not controllers) | What the roadmap names; lowest ceremony for four endpoints; the learning target. |
| New project | `Kenaz.Api` (`Microsoft.NET.Sdk.Web`), additive | The console stays the console; the API is a parallel adapter. Mirrors the Core-as-Model split. |
| Endpoint surface | Check-in CRUD + history only | Faithful to *"API over the data."* Insights stay in the console until M6 needs endpoints. YAGNI. |
| Talks to | `WellbeingJournal`, not the repository | Reuses all validation/upsert logic; the API holds no domain rules. |
| Delete | `WellbeingJournal.Delete`, built on `LoadAll`/`SaveAll` | No `ICheckInRepository` change; mirrors `AddOrUpdate`; free at this data scale. |
| Auth | Persisted bearer token + `IEndpointFilter`, constant-time compare | Honours the spec's "token-guarded"; a static single token needs no full auth scheme. Stable token = a contract M6 can rely on. |
| Token storage | `%APPDATA%\Kenaz\api-token`, Base64Url of 32 random bytes | Same folder convention; no exposure beyond the already-plaintext data beside it. |
| Binding | Kestrel `ListenLocalhost`, HTTP | Loopback-only per the spec; HTTPS is friction with no gain on loopback. |
| Config seam | `Kenaz:DbPath` / `TokenPath` / `Token` / `Port` via `IConfiguration` | Makes integration tests **and** manual curl runs hermetic — fixes the M4 "hardwired to `%APPDATA%`" pain. |
| Concurrency | `PRAGMA busy_timeout = 3000` per connection; no WAL | The named M5 revisit point, handled with one line; WAL is overkill for one user. |
| Port | `5247`, configurable | A fixed default M6/curl can target; overridable to avoid clashes. |
| DTOs | In `Kenaz.Api`, `record` types | The wire contract is the API's concern; Core's models stay clean. |
| Testing | `WebApplicationFactory<Program>` integration tests | Real HTTP through the real pipeline (auth, binding, serialization); the milestone's main new learning surface. |

---

## Out of scope (explicit cuts)

- **Insight endpoints** (7-day averages, streak, weekly review, sleep–mood pattern). Added when M6's PWA actually renders them, so the logic stays single-sourced in Core rather than guessed-at now.
- **Export/import over HTTP.** Console-only for now; `JsonCheckInArchive` already covers portability.
- **Repository-level `Upsert`/`Delete`.** The journal-level `Delete` suffices; revisit only with a concrete need the journal can't serve.
- **CORS, OpenAPI/Swagger, rate limiting.** CORS belongs with M6's known PWA origin. OpenAPI is a cheap future dev-aid, not needed for four curlable routes. Rate limiting is meaningless for a single local user.
- **HTTPS, WAL, schema versioning, accounts/multi-user, the PWA itself.** Against the local-first single-user stance (lines 69) or later milestones (M6).
- **Console rewiring.** The console keeps talking to Core directly; it does not become an API client.
- **API tests for the startup banner / `Program` wiring beyond the endpoints.** Matches the M1–M4 convention — composition roots are verified by running.

---

## Testing

Integration tests use ASP.NET's `WebApplicationFactory<Program>` (in-memory `TestServer` — no real port bound, so loopback config is irrelevant in tests). `Kenaz.Tests.csproj` gains `Microsoft.AspNetCore.Mvc.Testing` (10.0.x, matching the target framework), a `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, and a project reference to `Kenaz.Api`. A factory subclass overrides `Kenaz:DbPath` to a per-test temp database (same `Guid.NewGuid().ToString("N")` temp-folder pattern as the existing fixtures) and `Kenaz:Token` to a known value; `TearDown` deletes the folder and calls `SqliteConnection.ClearAllPools()` (the M4 Windows file-lock lesson). **~16 new tests, bringing the total to ~108.**

### `CheckInApiTests` (~13)
- `Get_checkins_without_token_returns_401`
- `Get_checkins_with_wrong_token_returns_401`
- `Get_checkins_on_empty_store_returns_200_and_empty_array`
- `Put_creates_checkin_then_get_returns_it` (round-trips all set fields)
- `Put_twice_updates_in_place_and_preserves_CreatedAt` (second PUT wins; `CreatedAt` stable, `UpdatedAt` advances)
- `Put_with_all_null_fields_returns_400`
- `Put_with_out_of_range_mood_returns_400`
- `Put_with_invalid_date_format_returns_400`
- `Get_unknown_date_returns_404`
- `Delete_existing_checkin_returns_204_then_get_returns_404`
- `Delete_unknown_date_returns_404`
- `Round_trips_decimal_sleep_and_null_fields` (PUT `sleep 7.5`, null energy/note → GET returns them exactly)
- `Get_history_orders_newest_first` (PUT several dates → GET is descending by date)

### `WellbeingJournalTests` additions (~3)
- `Delete_removes_the_date_and_returns_true`
- `Delete_absent_date_returns_false_and_writes_nothing`
- `Delete_only_removes_the_target_date` (other dates untouched)

---

## Verification (M5, end-to-end)

- `dotnet build Kenaz.slnx` — clean (0 warnings / 0 errors).
- `dotnet test Kenaz.slnx` — all NUnit tests pass (~108 total).
- **Manual (hermetic via the config seam — no real data touched):**
  `dotnet run --project Kenaz.Api -- --Kenaz:DbPath=C:\Temp\kenaz-m5\checkins.db --Kenaz:TokenPath=C:\Temp\kenaz-m5\api-token`
  reads the printed token, then with `$T` = that token:
  1. `curl http://127.0.0.1:5247/checkins` → `401`.
  2. `curl -H "Authorization: Bearer $T" http://127.0.0.1:5247/checkins` → `200` `[]`.
  3. `curl -X PUT -H "Authorization: Bearer $T" -H "Content-Type: application/json" -d '{"mood":7,"sleep":7.5}' http://127.0.0.1:5247/checkins/2026-05-31` → `200` with the resource.
  4. `GET /checkins/2026-05-31` → `200`; `GET /checkins/2020-01-01` → `404`; `PUT …/not-a-date` → `400`; `PUT` an empty body `{}` → `400`.
  5. `DELETE /checkins/2026-05-31` → `204`; the same `GET` → `404`.
- **MVC separation check:** `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'` — expected: no matches (M5 adds only `WellbeingJournal.Delete` in `Services/`, no IO).

---

## Self-review (spec coverage vs original milestone line 84)

- *ASP.NET Minimal API* → `Kenaz.Api`, four Minimal-API endpoints over `WellbeingJournal`.
- *over the data* → check-in CRUD + history against the live SQLite store; no new persistence, no duplicated logic.
- *loopback-only* → Kestrel `ListenLocalhost`; bearer-token filter on every endpoint; constant-time compare; the `Authorization`-header requirement also blocks browser-forged writes (line 69's "token-guarded… never a public multi-user service").

---

## How to proceed (after approval)

1. Hand off to **writing-plans** for `docs/superpowers/plans/2026-05-31-kenaz-m5-api.md`, TDD task-by-task, carrying as explicit acceptance criteria: the `401`/`400`/`404`/`204` contract, the constant-time token compare, the config seam (so tests are hermetic), `WellbeingJournal.Delete` semantics, and the `busy_timeout` refactor leaving console behaviour identical.
2. Execute task-by-task: TDD, one task = one commit, commits via GitHub Desktop (no `Co-Authored-By`).
3. **Learning mode applies** (per the original spec): each task carries a short `> Concept:` note on the new idea — Minimal API routing, route groups + `IEndpointFilter`, DI in a web host, model binding, `Results`/status codes, `WebApplicationFactory` integration testing, Kestrel loopback binding, `RandomNumberGenerator`, and constant-time comparison.
4. **README update** is part of the plan: a short "Local API (M5)" note — how to run it, that it is loopback-only and token-guarded, where the token lives (`%APPDATA%\Kenaz\api-token`), and that it is for the upcoming PWA rather than daily use yet.
