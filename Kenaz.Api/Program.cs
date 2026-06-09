using Kenaz.Api;
using Kenaz.Core;

var builder = WebApplication.CreateBuilder(args);

// Config seam — defaults to the real locations, overridable by tests and manual runs.
var dbPath    = builder.Configuration["Kenaz:DbPath"]    ?? SqliteCheckInRepository.DefaultFilePath();
var tokenPath = builder.Configuration["Kenaz:TokenPath"] ?? TokenStore.DefaultTokenPath();
var port      = int.TryParse(builder.Configuration["Kenaz:Port"], out var p) ? p : 5247;
// A known token (tests) skips the file entirely.
var token     = builder.Configuration["Kenaz:Token"] ?? TokenStore.GetOrCreate(tokenPath);

// Keep check-in dates (carried in request paths) and bodies out of the logs.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
// Quiet the framework's startup lifetime banner (Now listening / Application started / Content root)
// so the only startup notice is our own line below.
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

builder.WebHost.ConfigureKestrel(k => k.ListenLocalhost(port));   // 127.0.0.1 + [::1] only

builder.Services.AddSingleton<ICheckInRepository>(_ => new SqliteCheckInRepository(dbPath));
builder.Services.AddSingleton(sp => new WellbeingJournal(sp.GetRequiredService<ICheckInRepository>(), () => DateTimeOffset.Now));
builder.Services.AddSingleton(new ApiToken(token));
builder.Services.AddSingleton<WriteLock>();
builder.Services.AddSingleton(sp => new InsightsService(sp.GetRequiredService<WellbeingJournal>()));

var app = builder.Build();

// Unhandled storage failures → bodyless 500 (no developer exception page, no stack trace over the wire).
app.UseExceptionHandler(b => b.Run(ctx => { ctx.Response.StatusCode = 500; return Task.CompletedTask; }));

app.MapGroup("/checkins")
   .AddEndpointFilter<BearerTokenFilter>()
   .MapCheckInEndpoints();

app.MapGroup("/insights")
   .AddEndpointFilter<BearerTokenFilter>()
   .MapInsightsEndpoints();

// Token printed to stdout ONLY — never through ILogger / any log sink.
Console.WriteLine($"Kenaz API → http://127.0.0.1:{port}  (Authorization: Bearer {token})");

app.Run();

public partial class Program;
