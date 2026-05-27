using Kenaz.Core;

namespace Kenaz.Tests;

public class InsightTests
{
    // Anchored to local time so date derivation matches the journal on any machine.
    private static readonly DateTimeOffset Now = new DateTimeOffset(new DateTime(2026, 5, 22, 9, 0, 0, DateTimeKind.Local));
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.LocalDateTime);

    private WellbeingJournal _journal = null!;

    [SetUp]
    public void SetUp()
    {
        var repository = new InMemoryCheckInRepository();
        _journal = new WellbeingJournal(repository, () => Now);
    }

    private void Log(DateOnly date, int? mood = null, int? energy = null, decimal? sleep = null, string? note = null)
    {
        _journal.AddOrUpdate(date, mood, energy, sleep, note);
    }

    [Test]
    public void Average_excludes_null_values()
    {
        Log(Today, mood: 8);
        Log(Today.AddDays(-1), note: "skipped mood today-1");
        Log(Today.AddDays(-2), mood: 4);

        var average = _journal.Average(c => c.Mood, days: 7, now: Now);

        Assert.That(average, Is.EqualTo(6m));
    }

    [Test]
    public void Average_returns_null_when_window_has_no_values()
    {
        Log(Today, note: "note only, no scales");

        var average = _journal.Average(c => c.Mood, days: 7, now: Now);

        Assert.That(average, Is.Null);
    }

    [Test]
    public void Average_returns_null_when_journal_is_empty()
    {
        var average = _journal.Average(c => c.Mood, days: 7, now: Now);

        Assert.That(average, Is.Null);
    }

    [Test]
    public void Last7Days_includes_today()
    {
        Log(Today, mood: 5);

        Assert.That(_journal.Last7Days(Now), Has.Count.EqualTo(1));
    }

    [Test]
    public void Last7Days_includes_the_sixth_day_back()
    {
        Log(Today.AddDays(-6), mood: 5);

        Assert.That(_journal.Last7Days(Now), Has.Count.EqualTo(1));
    }

    [Test]
    public void Last7Days_excludes_the_seventh_day_back()
    {
        Log(Today.AddDays(-7), mood: 5);

        Assert.That(_journal.Last7Days(Now), Is.Empty);
    }

    [Test]
    public void StreakDays_is_zero_when_journal_is_empty()
    {
        Assert.That(_journal.StreakDays(Now), Is.EqualTo(0));
    }

    [Test]
    public void StreakDays_counts_consecutive_logged_days()
    {
        Log(Today, mood: 5);
        Log(Today.AddDays(-1), mood: 5);
        Log(Today.AddDays(-2), mood: 5);

        Assert.That(_journal.StreakDays(Now), Is.EqualTo(3));
    }

    [Test]
    public void StreakDays_does_not_reset_when_today_not_yet_logged()
    {
        Log(Today.AddDays(-1), mood: 5);
        Log(Today.AddDays(-2), mood: 5);

        Assert.That(_journal.StreakDays(Now), Is.EqualTo(2));
    }

    [Test]
    public void StreakDays_forgives_a_single_gap()
    {
        Log(Today, mood: 5);
        Log(Today.AddDays(-2), mood: 5);
        Log(Today.AddDays(-3), mood: 5);

        Assert.That(_journal.StreakDays(Now), Is.EqualTo(3));
    }

    [Test]
    public void StreakDays_breaks_on_two_consecutive_gaps()
    {
        Log(Today, mood: 5);
        Log(Today.AddDays(-3), mood: 5);

        Assert.That(_journal.StreakDays(Now), Is.EqualTo(1));
    }

    [Test]
    public void BestDay_returns_null_when_window_is_empty()
    {
        var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

        Assert.That(best, Is.Null);
    }

    [Test]
    public void BestDay_returns_null_when_no_day_in_window_has_the_selected_field()
    {
        Log(Today, note: "note only");
        Log(Today.AddDays(-1), note: "note only");

        var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

        Assert.That(best, Is.Null);
    }

    [Test]
    public void BestDay_picks_the_day_with_the_max_value()
    {
        Log(Today, mood: 5);
        Log(Today.AddDays(-1), mood: 9);
        Log(Today.AddDays(-2), mood: 3);

        var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

        Assert.That(best!.Date, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public void BestDay_ignores_days_where_the_selector_is_null()
    {
        Log(Today, mood: 5);
        Log(Today.AddDays(-1), note: "no mood today-1");
        Log(Today.AddDays(-2), mood: 7);

        var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

        Assert.That(best!.Date, Is.EqualTo(Today.AddDays(-2)));
    }

    [Test]
    public void BestDay_tie_breaker_picks_the_most_recent_date()
    {
        Log(Today.AddDays(-3), mood: 7);
        Log(Today.AddDays(-1), mood: 7);
        Log(Today.AddDays(-2), mood: 7);

        var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

        Assert.That(best!.Date, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public void BestDay_excludes_the_seventh_day_back()
    {
        Log(Today.AddDays(-7), mood: 9);
        Log(Today.AddDays(-1), mood: 3);

        var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

        Assert.That(best!.Date, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public void WorstDay_picks_the_day_with_the_min_value()
    {
        Log(Today, mood: 5);
        Log(Today.AddDays(-1), mood: 3);
        Log(Today.AddDays(-2), mood: 9);

        var worst = _journal.WorstDay(c => c.Mood, days: 7, now: Now);

        Assert.That(worst!.Date, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public void WorstDay_tie_breaker_picks_the_most_recent_date()
    {
        Log(Today.AddDays(-3), mood: 2);
        Log(Today.AddDays(-1), mood: 2);
        Log(Today.AddDays(-2), mood: 2);

        var worst = _journal.WorstDay(c => c.Mood, days: 7, now: Now);

        Assert.That(worst!.Date, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public void SleepMoodPattern_returns_zeros_and_nulls_on_empty_window()
    {
        var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

        Assert.That(pattern.LongSleepDays, Is.EqualTo(0));
        Assert.That(pattern.ShortSleepDays, Is.EqualTo(0));
        Assert.That(pattern.LongSleepMoodAverage, Is.Null);
        Assert.That(pattern.ShortSleepMoodAverage, Is.Null);
        Assert.That(pattern.IsConfident, Is.False);
    }

    [Test]
    public void SleepMoodPattern_excludes_days_missing_either_sleep_or_mood()
    {
        Log(Today, mood: 7, sleep: 8m);                  // qualified, long
        Log(Today.AddDays(-1), mood: 5);                  // mood only — excluded
        Log(Today.AddDays(-2), sleep: 8m);                // sleep only — excluded
        Log(Today.AddDays(-3), note: "neither");          // excluded

        var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

        Assert.That(pattern.LongSleepDays, Is.EqualTo(1));
        Assert.That(pattern.ShortSleepDays, Is.EqualTo(0));
    }

    [Test]
    public void SleepMoodPattern_buckets_at_thresholdHours_with_inclusive_lower_bound()
    {
        Log(Today, mood: 7, sleep: 7m);                   // exactly threshold → long
        Log(Today.AddDays(-1), mood: 6, sleep: 6.99m);    // below threshold → short

        var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

        Assert.That(pattern.LongSleepDays, Is.EqualTo(1));
        Assert.That(pattern.ShortSleepDays, Is.EqualTo(1));
    }

    [Test]
    public void SleepMoodPattern_is_not_confident_when_long_bucket_is_one_below_the_floor()
    {
        // 4 long + 5 short (4 = MinDaysPerBucketForConfidence - 1)
        for (var i = 0; i < 4; i++)
        {
            Log(Today.AddDays(-i), mood: 7, sleep: 8m);
        }
        for (var i = 4; i < 9; i++)
        {
            Log(Today.AddDays(-i), mood: 5, sleep: 6m);
        }

        var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

        Assert.That(pattern.LongSleepDays, Is.EqualTo(4));
        Assert.That(pattern.ShortSleepDays, Is.EqualTo(5));
        Assert.That(pattern.IsConfident, Is.False);
    }

    [Test]
    public void SleepMoodPattern_is_not_confident_when_short_bucket_is_one_below_the_floor()
    {
        // 5 long + 4 short
        for (var i = 0; i < 5; i++)
        {
            Log(Today.AddDays(-i), mood: 7, sleep: 8m);
        }
        for (var i = 5; i < 9; i++)
        {
            Log(Today.AddDays(-i), mood: 5, sleep: 6m);
        }

        var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

        Assert.That(pattern.LongSleepDays, Is.EqualTo(5));
        Assert.That(pattern.ShortSleepDays, Is.EqualTo(4));
        Assert.That(pattern.IsConfident, Is.False);
    }

    [Test]
    public void SleepMoodPattern_is_confident_when_both_buckets_meet_the_floor_exactly()
    {
        // 5 long + 5 short, mood differs so we can also check the averages
        for (var i = 0; i < 5; i++)
        {
            Log(Today.AddDays(-i), mood: 8, sleep: 8m);
        }
        for (var i = 5; i < 10; i++)
        {
            Log(Today.AddDays(-i), mood: 5, sleep: 6m);
        }

        var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

        Assert.That(pattern.LongSleepDays, Is.EqualTo(5));
        Assert.That(pattern.ShortSleepDays, Is.EqualTo(5));
        Assert.That(pattern.LongSleepMoodAverage, Is.EqualTo(8m));
        Assert.That(pattern.ShortSleepMoodAverage, Is.EqualTo(5m));
        Assert.That(pattern.IsConfident, Is.True);
    }
}
