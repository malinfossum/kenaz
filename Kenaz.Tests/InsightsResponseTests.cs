using Kenaz.Api;
using Kenaz.Core;

namespace Kenaz.Tests;

public class InsightsResponseTests
{
    [Test]
    public void From_flattens_summary_and_pattern_with_iso_dates_and_string_direction()
    {
        var brightest = new CheckIn(new DateOnly(2026, 5, 27), mood: 9, energy: 8, sleep: 7.5m, note: "bright",
            createdAt: DateTimeOffset.UnixEpoch, updatedAt: DateTimeOffset.UnixEpoch);
        var hardest = new CheckIn(new DateOnly(2026, 5, 26), mood: 3, energy: 4, sleep: 5m, note: "hard",
            createdAt: DateTimeOffset.UnixEpoch, updatedAt: DateTimeOffset.UnixEpoch);
        var pattern = new SleepMoodPattern(threshold: 7m, longSleepDays: 6, shortSleepDays: 5,
            longSleepMoodAverage: 8m, shortSleepMoodAverage: 6m, isConfident: true);
        var summary = new InsightsSummary(
            moodAverage: 7.5m, energyAverage: 6m, sleepAverage: 7m, streakDays: 4, hasWeekData: true,
            brightestDay: brightest, hardestDay: hardest, hasHighlights: true,
            sleepMood: pattern, showSleepTeaser: true, teaserDirection: SleepTeaserDirection.MoreSleepBetter);

        var dto = InsightsResponse.From(summary);

        Assert.That(dto.MoodAverage, Is.EqualTo(7.5m));
        Assert.That(dto.StreakDays, Is.EqualTo(4));
        Assert.That(dto.HasWeekData, Is.True);
        Assert.That(dto.BrightestDay!.Date, Is.EqualTo("2026-05-27"));
        Assert.That(dto.BrightestDay.Mood, Is.EqualTo(9));
        Assert.That(dto.HardestDay!.Date, Is.EqualTo("2026-05-26"));
        Assert.That(dto.SleepThreshold, Is.EqualTo(7m));
        Assert.That(dto.LongSleepDays, Is.EqualTo(6));
        Assert.That(dto.LongSleepMoodAverage, Is.EqualTo(8m));
        Assert.That(dto.ShortSleepDays, Is.EqualTo(5));
        Assert.That(dto.ShortSleepMoodAverage, Is.EqualTo(6m));
        Assert.That(dto.SleepPatternConfident, Is.True);
        Assert.That(dto.ShowSleepTeaser, Is.True);
        Assert.That(dto.TeaserDirection, Is.EqualTo("MoreSleepBetter"));
    }

    [Test]
    public void From_maps_absent_highlights_to_null()
    {
        var pattern = new SleepMoodPattern(threshold: 7m, longSleepDays: 0, shortSleepDays: 0,
            longSleepMoodAverage: null, shortSleepMoodAverage: null, isConfident: false);
        var summary = new InsightsSummary(
            moodAverage: null, energyAverage: null, sleepAverage: null, streakDays: 0, hasWeekData: false,
            brightestDay: null, hardestDay: null, hasHighlights: false,
            sleepMood: pattern, showSleepTeaser: false, teaserDirection: SleepTeaserDirection.None);

        var dto = InsightsResponse.From(summary);

        Assert.That(dto.BrightestDay, Is.Null);
        Assert.That(dto.HardestDay, Is.Null);
        Assert.That(dto.TeaserDirection, Is.EqualTo("None"));
    }
}
