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
}
