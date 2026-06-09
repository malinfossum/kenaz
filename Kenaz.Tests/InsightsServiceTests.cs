using Kenaz.Core;

namespace Kenaz.Tests;

public class InsightsServiceTests
{
    // Anchored to local time so date derivation matches the journal on any machine (same as InsightTests).
    private static readonly DateTimeOffset Now = new DateTimeOffset(new DateTime(2026, 5, 22, 9, 0, 0, DateTimeKind.Local));
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.LocalDateTime);

    private WellbeingJournal _journal = null!;
    private InsightsService _insights = null!;

    [SetUp]
    public void SetUp()
    {
        var repository = new InMemoryCheckInRepository();
        _journal = new WellbeingJournal(repository, () => Now);
        _insights = new InsightsService(_journal);
    }

    private void Log(DateOnly date, int? mood = null, int? energy = null, decimal? sleep = null, string? note = null)
    {
        _journal.AddOrUpdate(date, mood, energy, sleep, note);
    }

    [Test]
    public void Summarize_on_empty_store_has_no_week_data_and_null_averages()
    {
        var summary = _insights.Summarize(Now);

        Assert.That(summary.HasWeekData, Is.False);
        Assert.That(summary.MoodAverage, Is.Null);
        Assert.That(summary.EnergyAverage, Is.Null);
        Assert.That(summary.SleepAverage, Is.Null);
        Assert.That(summary.StreakDays, Is.EqualTo(0));
    }

    [Test]
    public void Summarize_computes_week_averages_over_the_7_day_window()
    {
        Log(Today, mood: 8, energy: 6, sleep: 7m);
        Log(Today.AddDays(-2), mood: 4, energy: 4, sleep: 5m);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.HasWeekData, Is.True);
        Assert.That(summary.MoodAverage, Is.EqualTo(6m));
        Assert.That(summary.EnergyAverage, Is.EqualTo(5m));
        Assert.That(summary.SleepAverage, Is.EqualTo(6m));
    }

    [Test]
    public void Summarize_passes_through_the_streak()
    {
        Log(Today, mood: 5);
        Log(Today.AddDays(-1), mood: 5);
        Log(Today.AddDays(-2), mood: 5);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.StreakDays, Is.EqualTo(3));
    }

    [Test]
    public void Summarize_highlights_available_when_two_distinct_moods()
    {
        Log(Today, mood: 9);
        Log(Today.AddDays(-1), mood: 3);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.HasHighlights, Is.True);
        Assert.That(summary.BrightestDay!.Date, Is.EqualTo(Today));
        Assert.That(summary.HardestDay!.Date, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public void Summarize_gates_highlights_when_all_moods_equal()
    {
        Log(Today, mood: 6);
        Log(Today.AddDays(-1), mood: 6);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.HasHighlights, Is.False);
        Assert.That(summary.BrightestDay, Is.Null);
        Assert.That(summary.HardestDay, Is.Null);
    }

    [Test]
    public void Summarize_gates_highlights_with_a_single_mood_day()
    {
        Log(Today, mood: 7);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.HasHighlights, Is.False);
        Assert.That(summary.BrightestDay, Is.Null);
        Assert.That(summary.HardestDay, Is.Null);
    }

    [Test]
    public void Summarize_highlights_use_the_7_day_window()
    {
        Log(Today.AddDays(-7), mood: 9);   // outside the window — must not become brightest
        Log(Today, mood: 5);
        Log(Today.AddDays(-1), mood: 3);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.HasHighlights, Is.True);
        Assert.That(summary.BrightestDay!.Date, Is.EqualTo(Today));
        Assert.That(summary.HardestDay!.Date, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public void Summarize_shows_teaser_when_confident_and_gap_at_least_one()
    {
        // 5 long-sleep days mood 8 (avg 8), 5 short-sleep days mood 7 (avg 7) → gap exactly 1.0
        for (var i = 0; i < 5; i++) Log(Today.AddDays(-i), mood: 8, sleep: 8m);
        for (var i = 5; i < 10; i++) Log(Today.AddDays(-i), mood: 7, sleep: 6m);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.SleepMood.IsConfident, Is.True);
        Assert.That(summary.ShowSleepTeaser, Is.True);
        Assert.That(summary.TeaserDirection, Is.EqualTo(SleepTeaserDirection.MoreSleepBetter));
    }

    [Test]
    public void Summarize_hides_teaser_when_gap_below_one()
    {
        // long avg 8, short avg 7.2 ({8,7,7,7,7}) → gap 0.8 < 1.0 → hidden
        for (var i = 0; i < 5; i++) Log(Today.AddDays(-i), mood: 8, sleep: 8m);
        Log(Today.AddDays(-5), mood: 8, sleep: 6m);
        for (var i = 6; i < 10; i++) Log(Today.AddDays(-i), mood: 7, sleep: 6m);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.SleepMood.IsConfident, Is.True);
        Assert.That(summary.ShowSleepTeaser, Is.False);
        Assert.That(summary.TeaserDirection, Is.EqualTo(SleepTeaserDirection.None));
    }

    [Test]
    public void Summarize_hides_teaser_when_not_confident()
    {
        // 4 long + 5 short (long bucket one below the floor of 5) — large gap, but not confident
        for (var i = 0; i < 4; i++) Log(Today.AddDays(-i), mood: 9, sleep: 8m);
        for (var i = 4; i < 9; i++) Log(Today.AddDays(-i), mood: 3, sleep: 6m);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.SleepMood.IsConfident, Is.False);
        Assert.That(summary.ShowSleepTeaser, Is.False);
        Assert.That(summary.TeaserDirection, Is.EqualTo(SleepTeaserDirection.None));
    }

    [Test]
    public void Summarize_teaser_direction_is_less_sleep_better_at_negative_gap()
    {
        // long avg 7, short avg 8 → gap -1.0 → shown, shorter nights felt better
        for (var i = 0; i < 5; i++) Log(Today.AddDays(-i), mood: 7, sleep: 8m);
        for (var i = 5; i < 10; i++) Log(Today.AddDays(-i), mood: 8, sleep: 6m);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.ShowSleepTeaser, Is.True);
        Assert.That(summary.TeaserDirection, Is.EqualTo(SleepTeaserDirection.LessSleepBetter));
    }

    [Test]
    public void Summarize_pattern_confident_carries_threshold_counts_and_averages()
    {
        for (var i = 0; i < 5; i++) Log(Today.AddDays(-i), mood: 8, sleep: 8m);
        for (var i = 5; i < 10; i++) Log(Today.AddDays(-i), mood: 5, sleep: 6m);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.SleepMood.IsConfident, Is.True);
        Assert.That(summary.SleepMood.Threshold, Is.EqualTo(7m));
        Assert.That(summary.SleepMood.LongSleepDays, Is.EqualTo(5));
        Assert.That(summary.SleepMood.ShortSleepDays, Is.EqualTo(5));
        Assert.That(summary.SleepMood.LongSleepMoodAverage, Is.EqualTo(8m));
        Assert.That(summary.SleepMood.ShortSleepMoodAverage, Is.EqualTo(5m));
    }

    [Test]
    public void Summarize_pattern_not_confident_below_min_days_per_bucket()
    {
        // 4 long + 4 short — both below the floor of 5
        for (var i = 0; i < 4; i++) Log(Today.AddDays(-i), mood: 8, sleep: 8m);
        for (var i = 4; i < 8; i++) Log(Today.AddDays(-i), mood: 5, sleep: 6m);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.SleepMood.IsConfident, Is.False);
    }
}
