namespace Kenaz.Core;

/// <summary>
/// A bucket-compare of mood across nights of more vs less sleep. Carries the counts and averages
/// so the view never has to reimplement the confidence rule. Days in either bucket are *qualified
/// days* — days with both mood and sleep present.
/// </summary>
public sealed class SleepMoodPattern
{
    public SleepMoodPattern(
        decimal threshold,
        int longSleepDays,
        int shortSleepDays,
        decimal? longSleepMoodAverage,
        decimal? shortSleepMoodAverage,
        bool isConfident)
    {
        Threshold = threshold;
        LongSleepDays = longSleepDays;
        ShortSleepDays = shortSleepDays;
        LongSleepMoodAverage = longSleepMoodAverage;
        ShortSleepMoodAverage = shortSleepMoodAverage;
        IsConfident = isConfident;
    }

    public decimal Threshold { get; }
    public int LongSleepDays { get; }
    public int ShortSleepDays { get; }
    public decimal? LongSleepMoodAverage { get; }
    public decimal? ShortSleepMoodAverage { get; }
    public bool IsConfident { get; }
}
