namespace Kenaz.Core;

/// <summary>
/// A read-only snapshot of the insights both clients render. Composed by <see cref="InsightsService"/>
/// from the journal's existing aggregates; it also carries the gating flags (what is showable) so a
/// View only chooses words, never re-derives a threshold.
/// </summary>
public sealed class InsightsSummary
{
    public InsightsSummary(
        decimal? moodAverage,
        decimal? energyAverage,
        decimal? sleepAverage,
        int streakDays,
        bool hasWeekData,
        CheckIn? brightestDay,
        CheckIn? hardestDay,
        bool hasHighlights,
        SleepMoodPattern sleepMood,
        bool showSleepTeaser,
        SleepTeaserDirection teaserDirection)
    {
        MoodAverage = moodAverage;
        EnergyAverage = energyAverage;
        SleepAverage = sleepAverage;
        StreakDays = streakDays;
        HasWeekData = hasWeekData;
        BrightestDay = brightestDay;
        HardestDay = hardestDay;
        HasHighlights = hasHighlights;
        SleepMood = sleepMood;
        ShowSleepTeaser = showSleepTeaser;
        TeaserDirection = teaserDirection;
    }

    // 7-day glance (null = not enough data for that metric)
    public decimal? MoodAverage { get; }
    public decimal? EnergyAverage { get; }
    public decimal? SleepAverage { get; }
    public int StreakDays { get; }
    public bool HasWeekData { get; }

    // 7-day mood highlights (null unless HasHighlights)
    public CheckIn? BrightestDay { get; }
    public CheckIn? HardestDay { get; }
    public bool HasHighlights { get; }

    // 30-day sleep–mood pattern (the existing M3 value type)
    public SleepMoodPattern SleepMood { get; }

    // Today-screen teaser gate (derived from the pattern)
    public bool ShowSleepTeaser { get; }
    public SleepTeaserDirection TeaserDirection { get; }
}

/// <summary>Which way the sleep–mood teaser leans, or None when it should not show.</summary>
public enum SleepTeaserDirection
{
    None,
    MoreSleepBetter,
    LessSleepBetter
}
