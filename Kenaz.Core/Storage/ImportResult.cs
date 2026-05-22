namespace Kenaz.Core;

/// <summary>The outcome of an import: the records that survived validation, and how many were dropped.</summary>
public sealed class ImportResult
{
    public ImportResult(IReadOnlyList<CheckIn> records, int skipped)
    {
        Records = records;
        Skipped = skipped;
    }

    public IReadOnlyList<CheckIn> Records { get; }
    public int Skipped { get; }
}
