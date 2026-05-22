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

    /// <summary>
    /// Folds imported check-ins into the store, keyed by date. A new date is added; an existing date is
    /// replaced only when the incoming record is more recently updated; otherwise it is left unchanged.
    /// Imported timestamps are preserved (this never routes through <see cref="AddOrUpdate"/>).
    /// </summary>
    public MergeResult Merge(IReadOnlyList<CheckIn> incoming)
    {
        var byDate = _repository.LoadAll().ToDictionary(c => c.Date);

        var added = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var candidate in incoming)
        {
            if (!byDate.TryGetValue(candidate.Date, out var existing))
            {
                byDate[candidate.Date] = candidate;
                added++;
            }
            else if (candidate.UpdatedAt > existing.UpdatedAt)
            {
                byDate[candidate.Date] = candidate;
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        _repository.SaveAll(byDate.Values.ToList());
        return new MergeResult(added, updated, unchanged);
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

    /// <summary>The check-ins from the 7 calendar days ending today (today and the six days before), newest first.</summary>
    public IReadOnlyList<CheckIn> Last7Days(DateTimeOffset now)
    {
        var today = Today(now);
        var start = today.AddDays(-6);

        return _repository.LoadAll()
            .Where(c => c.Date >= start && c.Date <= today)
            .OrderByDescending(c => c.Date)
            .ToList();
    }

    /// <summary>
    /// Average of the selected field across the window of <paramref name="days"/> days ending today.
    /// Null values are skipped; returns null when the window has no values (never averages an empty set).
    /// </summary>
    public decimal? Average(Func<CheckIn, decimal?> selector, int days, DateTimeOffset now)
    {
        var today = Today(now);
        var start = today.AddDays(-(days - 1));

        // The nullable-decimal Average overload skips nulls and returns null for an empty
        // window, so a skipped field never counts as zero and an empty window never throws.
        return _repository.LoadAll()
            .Where(c => c.Date >= start && c.Date <= today)
            .Select(selector)
            .Average();
    }

    /// <summary>
    /// Consecutive logged days, counted back from the most recent logged day. An unlogged today
    /// is not a miss; a single gap is forgiven; two missed days in a row end the streak.
    /// </summary>
    public int StreakDays(DateTimeOffset now)
    {
        var today = Today(now);
        var logged = _repository.LoadAll()
            .Select(c => c.Date)
            .Where(date => date <= today)
            .ToHashSet();

        if (logged.Count == 0)
        {
            return 0;
        }

        var cursor = logged.Max();
        var streak = 0;

        while (true)
        {
            if (logged.Contains(cursor))
            {
                streak++;
                cursor = cursor.AddDays(-1);
            }
            else if (logged.Contains(cursor.AddDays(-1)))
            {
                cursor = cursor.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    private static DateOnly Today(DateTimeOffset now)
    {
        return DateOnly.FromDateTime(now.LocalDateTime);
    }
}
