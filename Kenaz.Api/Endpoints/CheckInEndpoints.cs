using System.Globalization;
using Kenaz.Core;

namespace Kenaz.Api;

public static class CheckInEndpoints
{
    public static RouteGroupBuilder MapCheckInEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", (WellbeingJournal journal) =>
            Results.Ok(journal.History().Select(CheckInResponse.From)));

        group.MapGet("/{date}", (string date, WellbeingJournal journal) =>
            TryDate(date, out var d)
                ? journal.GetByDate(d) is { } c ? Results.Ok(CheckInResponse.From(c)) : Results.NotFound()
                : Results.BadRequest("Date must be yyyy-MM-dd."));

        group.MapPut("/{date}", async (string date, UpsertCheckInRequest? body, WellbeingJournal journal, WriteLock writeLock) =>
        {
            if (!TryDate(date, out var d))
            {
                return Results.BadRequest("Date must be yyyy-MM-dd.");
            }

            if (body is null)
            {
                return Results.BadRequest("Request body is required.");
            }

            await writeLock.WaitAsync();
            try
            {
                var saved = journal.AddOrUpdate(d, body.Mood, body.Energy, body.Sleep, body.Note);
                return Results.Ok(CheckInResponse.From(saved));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            finally
            {
                writeLock.Release();
            }
        });

        group.MapDelete("/{date}", async (string date, WellbeingJournal journal, WriteLock writeLock) =>
        {
            if (!TryDate(date, out var d))
            {
                return Results.BadRequest("Date must be yyyy-MM-dd.");
            }

            await writeLock.WaitAsync();
            try
            {
                return journal.Delete(d) ? Results.NoContent() : Results.NotFound();
            }
            finally
            {
                writeLock.Release();
            }
        });

        return group;
    }

    private static bool TryDate(string date, out DateOnly result) =>
        DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}
