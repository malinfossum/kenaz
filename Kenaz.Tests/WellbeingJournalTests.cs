using Kenaz.Core;

namespace Kenaz.Tests;

public class WellbeingJournalTests
{
    private static readonly DateOnly Day = new DateOnly(2026, 5, 22);

    private DateTimeOffset _clock;
    private WellbeingJournal _journal = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new DateTimeOffset(2026, 5, 22, 9, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryCheckInRepository();
        _journal = new WellbeingJournal(repository, () => _clock);
    }

    [Test]
    public void AddOrUpdate_creates_a_new_check_in()
    {
        var result = _journal.AddOrUpdate(Day, mood: 6, energy: null, sleep: null, note: null);

        Assert.That(result.Mood, Is.EqualTo(6));
        Assert.That(_journal.History(), Has.Count.EqualTo(1));
    }

    [Test]
    public void AddOrUpdate_on_same_date_edits_instead_of_duplicating()
    {
        _journal.AddOrUpdate(Day, mood: 6, energy: null, sleep: null, note: null);
        _clock = _clock.AddHours(2);
        _journal.AddOrUpdate(Day, mood: 9, energy: null, sleep: null, note: null);

        Assert.That(_journal.History(), Has.Count.EqualTo(1));
        Assert.That(_journal.GetByDate(Day)!.Mood, Is.EqualTo(9));
    }

    [Test]
    public void AddOrUpdate_edit_preserves_CreatedAt_and_advances_UpdatedAt()
    {
        var createdAt = _clock;
        _journal.AddOrUpdate(Day, mood: 6, energy: null, sleep: null, note: null);
        _clock = _clock.AddHours(2);
        _journal.AddOrUpdate(Day, mood: 9, energy: null, sleep: null, note: null);

        var checkIn = _journal.GetByDate(Day)!;
        Assert.That(checkIn.CreatedAt, Is.EqualTo(createdAt));
        Assert.That(checkIn.UpdatedAt, Is.EqualTo(createdAt.AddHours(2)));
    }

    [Test]
    public void AddOrUpdate_can_edit_a_backdated_day()
    {
        var yesterday = Day.AddDays(-1);
        _journal.AddOrUpdate(Day, mood: 5, energy: null, sleep: null, note: null);
        _journal.AddOrUpdate(yesterday, mood: 3, energy: null, sleep: null, note: null);
        _journal.AddOrUpdate(yesterday, mood: 8, energy: null, sleep: null, note: null);

        Assert.That(_journal.History(), Has.Count.EqualTo(2));
        Assert.That(_journal.GetByDate(yesterday)!.Mood, Is.EqualTo(8));
        Assert.That(_journal.GetByDate(Day)!.Mood, Is.EqualTo(5));
    }

    [Test]
    public void AddOrUpdate_rejects_all_empty()
    {
        Assert.That(
            () => _journal.AddOrUpdate(Day, mood: null, energy: null, sleep: null, note: null),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void GetByDate_returns_null_when_no_check_in_for_that_day()
    {
        Assert.That(_journal.GetByDate(Day), Is.Null);
    }

    [Test]
    public void History_returns_check_ins_newest_first()
    {
        _journal.AddOrUpdate(Day.AddDays(-2), mood: 1, energy: null, sleep: null, note: null);
        _journal.AddOrUpdate(Day, mood: 2, energy: null, sleep: null, note: null);
        _journal.AddOrUpdate(Day.AddDays(-1), mood: 3, energy: null, sleep: null, note: null);

        var history = _journal.History();

        Assert.That(history[0].Date, Is.EqualTo(Day));
        Assert.That(history[1].Date, Is.EqualTo(Day.AddDays(-1)));
        Assert.That(history[2].Date, Is.EqualTo(Day.AddDays(-2)));
    }

    [Test]
    public void Merge_adds_check_ins_for_new_dates()
    {
        var day = Day.AddDays(-2);
        var incoming = new[] { new CheckIn(day, mood: 5, energy: null, sleep: null, note: null, createdAt: _clock, updatedAt: _clock) };

        var result = _journal.Merge(incoming);

        Assert.That(result.Added, Is.EqualTo(1));
        Assert.That(_journal.GetByDate(day), Is.Not.Null);
    }

    [Test]
    public void Merge_updates_when_incoming_is_newer()
    {
        _journal.AddOrUpdate(Day, mood: 3, energy: null, sleep: null, note: null);
        var newer = new CheckIn(Day, mood: 9, energy: null, sleep: null, note: null, createdAt: _clock, updatedAt: _clock.AddHours(1));

        var result = _journal.Merge(new[] { newer });

        Assert.That(result.Updated, Is.EqualTo(1));
        Assert.That(_journal.GetByDate(Day)!.Mood, Is.EqualTo(9));
    }

    [Test]
    public void Merge_keeps_existing_when_incoming_is_older()
    {
        _journal.AddOrUpdate(Day, mood: 3, energy: null, sleep: null, note: null);
        var older = new CheckIn(Day, mood: 9, energy: null, sleep: null, note: null, createdAt: _clock, updatedAt: _clock.AddHours(-1));

        var result = _journal.Merge(new[] { older });

        Assert.That(result.Unchanged, Is.EqualTo(1));
        Assert.That(_journal.GetByDate(Day)!.Mood, Is.EqualTo(3));
    }
}
