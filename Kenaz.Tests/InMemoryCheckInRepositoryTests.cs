using Kenaz.Core;

namespace Kenaz.Tests;

public class InMemoryCheckInRepositoryTests
{
    private static readonly DateOnly Day = new DateOnly(2026, 5, 22);
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 5, 22, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void LoadAll_on_new_repository_is_empty()
    {
        ICheckInRepository repo = new InMemoryCheckInRepository();

        Assert.That(repo.LoadAll(), Is.Empty);
    }

    [Test]
    public void SaveAll_then_LoadAll_returns_saved_check_ins()
    {
        ICheckInRepository repo = new InMemoryCheckInRepository();
        var checkIn = new CheckIn(Day, mood: 6, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now);

        repo.SaveAll(new[] { checkIn });

        Assert.That(repo.LoadAll(), Has.Count.EqualTo(1));
        Assert.That(repo.LoadAll()[0].Mood, Is.EqualTo(6));
    }

    [Test]
    public void SaveAll_replaces_previous_contents()
    {
        ICheckInRepository repo = new InMemoryCheckInRepository();
        var first = new CheckIn(Day, mood: 6, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now);
        var second = new CheckIn(Day, mood: 9, energy: null, sleep: null, note: null, createdAt: Now, updatedAt: Now);

        repo.SaveAll(new[] { first });
        repo.SaveAll(new[] { second });

        Assert.That(repo.LoadAll(), Has.Count.EqualTo(1));
        Assert.That(repo.LoadAll()[0].Mood, Is.EqualTo(9));
    }
}
