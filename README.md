# Kenaz

A private, local-first daily wellbeing check-in: log mood, energy, and sleep, and see your patterns over time. *Bring it into the light.*

**Kenaz** (*KEN-ahz*) is the Elder Futhark rune for *torch*: to spark, to bring into the light. It's the fire-family sibling to [Ignite](https://github.com/malinfossum/ignite) — where Ignite is a small flame, kept going, Kenaz is the torch you hold up to see your week clearly.

## What it does

Check in for today (mood, energy, sleep, and a note — each optional), see today against your last 7 days with a gentle streak, open a weekly review, browse your history, and export or import your check-ins. Everything is stored locally in `%APPDATA%\Kenaz` — nothing leaves your machine.

## Stack

C# (.NET): domain core, console front-end, loopback HTTP API, NUnit tests. Mobile-first web app served by the API.

## Run

Console app:

```powershell
dotnet run --project Kenaz.Console
```

Web app (build once, then run the API):

```powershell
cd Kenaz.Web
npm install
npm run build
cd ..
dotnet run --project Kenaz.Api
```

Open `http://127.0.0.1:5247` and paste the token from the API's startup banner. The API binds to loopback only, and every request needs that token.

## License

Apache License 2.0 — see [LICENSE](LICENSE).
