using System.Globalization;
using Kenaz.Core;

namespace Kenaz.Api;

/// <summary>A brightest/hardest day on the wire — date as yyyy-MM-dd, no note.</summary>
public record DayHighlight(string Date, int? Mood, int? Energy, decimal? Sleep);

/// <summary>The wire shape of <see cref="InsightsSummary"/>: flattened, dates as yyyy-MM-dd, enum as string.</summary>
public record InsightsResponse(
    decimal? MoodAverage,
    decimal? EnergyAverage,
    decimal? SleepAverage,
    int StreakDays,
    bool HasWeekData,
    DayHighlight? BrightestDay,
    DayHighlight? HardestDay,
    bool HasHighlights,
    decimal SleepThreshold,
    int LongSleepDays,
    int ShortSleepDays,
    decimal? LongSleepMoodAverage,
    decimal? ShortSleepMoodAverage,
    bool SleepPatternConfident,
    bool ShowSleepTeaser,
    string TeaserDirection)
{
    public static InsightsResponse From(InsightsSummary s) => new InsightsResponse(
        MoodAverage: s.MoodAverage,
        EnergyAverage: s.EnergyAverage,
        SleepAverage: s.SleepAverage,
        StreakDays: s.StreakDays,
        HasWeekData: s.HasWeekData,
        BrightestDay: ToHighlight(s.BrightestDay),
        HardestDay: ToHighlight(s.HardestDay),
        HasHighlights: s.HasHighlights,
        SleepThreshold: s.SleepMood.Threshold,
        LongSleepDays: s.SleepMood.LongSleepDays,
        ShortSleepDays: s.SleepMood.ShortSleepDays,
        LongSleepMoodAverage: s.SleepMood.LongSleepMoodAverage,
        ShortSleepMoodAverage: s.SleepMood.ShortSleepMoodAverage,
        SleepPatternConfident: s.SleepMood.IsConfident,
        ShowSleepTeaser: s.ShowSleepTeaser,
        TeaserDirection: s.TeaserDirection.ToString());

    private static DayHighlight? ToHighlight(CheckIn? c) => c is null
        ? null
        : new DayHighlight(c.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), c.Mood, c.Energy, c.Sleep);
}
