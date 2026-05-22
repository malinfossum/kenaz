namespace Kenaz.Core;

/// <summary>
/// The journal coordinates check-ins on top of a repository. It owns the one-per-date rule
/// and derives "today" from an injected clock so behaviour is deterministic in tests.
/// </summary>
public class WellbeingJournal
{
    private readonly ICheckInRepository _repository;
    private readonly Func<DateTimeOffset> _now;

    public WellbeingJournal(ICheckInRepository repository, Func<DateTimeOffset> now)
    {
        _repository = repository;
        _now = now;
    }

    public CheckIn AddOrUpdate(DateOnly date, int? mood, int? energy, decimal? sleep, string? note)
    {
        var now = _now();
        var checkIns = _repository.LoadAll().ToList();
        var existing = checkIns.FirstOrDefault(c => c.Date == date);

        if (existing is null)
        {
            var created = new CheckIn(date, mood, energy, sleep, note, createdAt: now, updatedAt: now);
            checkIns.Add(created);
            _repository.SaveAll(checkIns);
            return created;
        }

        existing.Update(mood, energy, sleep, note, updatedAt: now);
        _repository.SaveAll(checkIns);
        return existing;
    }

    public CheckIn? GetByDate(DateOnly date)
    {
        return _repository.LoadAll().FirstOrDefault(c => c.Date == date);
    }

    public IReadOnlyList<CheckIn> History()
    {
        return _repository.LoadAll()
            .OrderByDescending(c => c.Date)
            .ToList();
    }
}
