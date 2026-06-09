namespace Kenaz.Core;

/// <summary>
/// Composes the journal's existing read methods into one <see cref="InsightsSummary"/> and owns the
/// gating that decides what is showable. Read-only; takes <c>now</c> as a parameter (no injected clock)
/// so gating boundaries are deterministic in tests. Uses the same windows the console uses: 7 days for
/// the week glance and highlights, 30 days for the sleep–mood pattern.
/// </summary>
public sealed class InsightsService
{
    private const int WeekDays = 7;
    private const int PatternDays = 30;

    private readonly WellbeingJournal _journal;

    public InsightsService(WellbeingJournal journal)
    {
        _journal = journal;
    }

    public InsightsSummary Summarize(DateTimeOffset now)
    {
        var hasWeekData = _journal.Last7Days(now).Count > 0;

        // Highlights (Task 2) and the real pattern + teaser (Task 3) are stubbed here.
        var stubbedPattern = new SleepMoodPattern(
            threshold: WellbeingJournal.DefaultSleepThresholdHours,
            longSleepDays: 0,
            shortSleepDays: 0,
            longSleepMoodAverage: null,
            shortSleepMoodAverage: null,
            isConfident: false);

        return new InsightsSummary(
            moodAverage: _journal.Average(c => c.Mood, WeekDays, now),
            energyAverage: _journal.Average(c => c.Energy, WeekDays, now),
            sleepAverage: _journal.Average(c => c.Sleep, WeekDays, now),
            streakDays: _journal.StreakDays(now),
            hasWeekData: hasWeekData,
            brightestDay: null,
            hardestDay: null,
            hasHighlights: false,
            sleepMood: stubbedPattern,
            showSleepTeaser: false,
            teaserDirection: SleepTeaserDirection.None);
    }
}
