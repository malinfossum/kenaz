namespace Kenaz.Core;

/// <summary>
/// Storage contract for check-ins. M1 keeps it list-in / list-out; the journal depends
/// on this abstraction so the backing store (JSON now, SQLite later) can change behind it.
/// </summary>
public interface ICheckInRepository
{
    IReadOnlyList<CheckIn> LoadAll();

    void SaveAll(IReadOnlyList<CheckIn> checkIns);
}
