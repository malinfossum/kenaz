using Kenaz.Core;

namespace Kenaz.Tests;

public class CheckInTests
{
    private static readonly DateOnly Today = new DateOnly(2026, 5, 22);
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 5, 22, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Constructor_with_valid_values_sets_all_fields()
    {
        var checkIn = new CheckIn(Today, mood: 7, energy: 6, sleep: 7.5m, note: "ok day", createdAt: Now, updatedAt: Now);

        Assert.That(checkIn.Date, Is.EqualTo(Today));
        Assert.That(checkIn.Mood, Is.EqualTo(7));
        Assert.That(checkIn.Energy, Is.EqualTo(6));
        Assert.That(checkIn.Sleep, Is.EqualTo(7.5m));
        Assert.That(checkIn.Note, Is.EqualTo("ok day"));
        Assert.That(checkIn.CreatedAt, Is.EqualTo(Now));
        Assert.That(checkIn.UpdatedAt, Is.EqualTo(Now));
    }

    [Test]
    public void Constructor_rejects_mood_below_1()
    {
        Assert.That(
            () => new CheckIn(Today, mood: 0, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_rejects_mood_above_10()
    {
        Assert.That(
            () => new CheckIn(Today, mood: 11, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_rejects_negative_sleep()
    {
        Assert.That(
            () => new CheckIn(Today, mood: null, energy: null, sleep: -1m, note: null, createdAt: Now, updatedAt: Now),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_rejects_sleep_over_24()
    {
        Assert.That(
            () => new CheckIn(Today, mood: null, energy: null, sleep: 25m, note: null, createdAt: Now, updatedAt: Now),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_rejects_all_empty()
    {
        Assert.That(
            () => new CheckIn(Today, mood: null, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_rejects_whitespace_only_note_with_no_other_fields()
    {
        Assert.That(
            () => new CheckIn(Today, mood: null, energy: null, sleep: null, note: "   ", createdAt: Now, updatedAt: Now),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_allows_single_scale_field()
    {
        var checkIn = new CheckIn(Today, mood: 5, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now);

        Assert.That(checkIn.Mood, Is.EqualTo(5));
    }

    [Test]
    public void Constructor_allows_note_only()
    {
        var checkIn = new CheckIn(Today, mood: null, energy: null, sleep: null, note: "just a note", createdAt: Now, updatedAt: Now);

        Assert.That(checkIn.Note, Is.EqualTo("just a note"));
    }

    [Test]
    public void Constructor_preserves_nulls_and_does_not_coerce_to_zero()
    {
        var checkIn = new CheckIn(Today, mood: 5, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now);

        Assert.That(checkIn.Energy, Is.Null);
        Assert.That(checkIn.Sleep, Is.Null);
    }

    [Test]
    public void Constructor_accepts_scale_boundaries()
    {
        var checkIn = new CheckIn(Today, mood: 1, energy: 10, sleep: null, note: null, createdAt: Now, updatedAt: Now);

        Assert.That(checkIn.Mood, Is.EqualTo(1));
        Assert.That(checkIn.Energy, Is.EqualTo(10));
    }

    [Test]
    public void Constructor_accepts_sleep_boundaries()
    {
        var none = new CheckIn(Today, mood: null, energy: null, sleep: 0m, note: null, createdAt: Now, updatedAt: Now);
        var full = new CheckIn(Today, mood: null, energy: null, sleep: 24m, note: null, createdAt: Now, updatedAt: Now);

        Assert.That(none.Sleep, Is.EqualTo(0m));
        Assert.That(full.Sleep, Is.EqualTo(24m));
    }

    [Test]
    public void Update_preserves_CreatedAt_and_advances_UpdatedAt()
    {
        var checkIn = new CheckIn(Today, mood: 5, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now);
        var later = Now.AddHours(3);

        checkIn.Update(mood: 8, energy: 7, sleep: null, note: null, updatedAt: later);

        Assert.That(checkIn.Mood, Is.EqualTo(8));
        Assert.That(checkIn.Energy, Is.EqualTo(7));
        Assert.That(checkIn.CreatedAt, Is.EqualTo(Now));
        Assert.That(checkIn.UpdatedAt, Is.EqualTo(later));
    }

    [Test]
    public void Update_rejects_all_empty()
    {
        var checkIn = new CheckIn(Today, mood: 5, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now);

        Assert.That(
            () => checkIn.Update(mood: null, energy: null, sleep: null, note: null, updatedAt: Now.AddHours(1)),
            Throws.TypeOf<ArgumentException>());
    }
}
