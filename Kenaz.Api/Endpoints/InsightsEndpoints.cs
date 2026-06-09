using Kenaz.Core;

namespace Kenaz.Api;

public static class InsightsEndpoints
{
    public static RouteGroupBuilder MapInsightsEndpoints(this RouteGroupBuilder group)
    {
        // Read-only: no WriteLock. Summarizes over the current wall clock.
        group.MapGet("/", (InsightsService insights) =>
            Results.Ok(InsightsResponse.From(insights.Summarize(DateTimeOffset.Now))));

        return group;
    }
}
