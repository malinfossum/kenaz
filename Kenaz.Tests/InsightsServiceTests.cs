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
}
