namespace Kenaz.Core;

/// <summary>How a merge resolved: dates newly added, existing dates replaced by a newer record, and dates left as they were.</summary>
public sealed class MergeResult
{
    public MergeResult(int added, int updated, int unchanged)
    {
        Added = added;
        Updated = updated;
        Unchanged = unchanged;
    }

    public int Added { get; }
    public int Updated { get; }
    public int Unchanged { get; }
}
