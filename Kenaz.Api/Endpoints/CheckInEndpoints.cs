using Kenaz.Core;

namespace Kenaz.Api;

public static class CheckInEndpoints
{
    public static RouteGroupBuilder MapCheckInEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", (WellbeingJournal journal) =>
            Results.Ok(journal.History().Select(CheckInResponse.From)));

        return group;
    }
}
