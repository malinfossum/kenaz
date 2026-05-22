namespace Kenaz.Core;

/// <summary>
/// A single day's wellbeing check-in. Scales and note are optional (null = skipped,
/// never zero). At least one field must be present; the constructor is the only gate
/// that lets a valid check-in exist.
/// </summary>
public class CheckIn
{
    public DateOnly Date { get; }
    public int? Mood { get; private set; }
    public int? Energy { get; private set; }
    public decimal? Sleep { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public CheckIn(
        DateOnly date,
        int? mood,
        int? energy,
        decimal? sleep,
        string? note,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Validate(mood, energy, sleep, note);

        Date = date;
        Mood = mood;
        Energy = energy;
        Sleep = sleep;
        Note = note;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public void Update(int? mood, int? energy, decimal? sleep, string? note, DateTimeOffset updatedAt)
    {
        Validate(mood, energy, sleep, note);

        Mood = mood;
        Energy = energy;
        Sleep = sleep;
        Note = note;
        UpdatedAt = updatedAt;
    }

    private static void Validate(int? mood, int? energy, decimal? sleep, string? note)
    {
        if (mood.HasValue && (mood.Value < 1 || mood.Value > 10))
        {
            throw new ArgumentException("Mood must be between 1 and 10 when provided.", nameof(mood));
        }

        if (energy.HasValue && (energy.Value < 1 || energy.Value > 10))
        {
            throw new ArgumentException("Energy must be between 1 and 10 when provided.", nameof(energy));
        }

        if (sleep.HasValue && (sleep.Value < 0 || sleep.Value > 24))
        {
            throw new ArgumentException("Sleep must be between 0 and 24 hours when provided.", nameof(sleep));
        }

        if (!mood.HasValue && !energy.HasValue && !sleep.HasValue && string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("A check-in needs at least one of: mood, energy, sleep, or a note.");
        }
    }
}
