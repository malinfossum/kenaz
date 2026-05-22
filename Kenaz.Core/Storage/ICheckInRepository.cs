namespace Kenaz.Core;

/// <summary>
/// Storage contract for the live check-in store: list-in / list-out, nothing more. The backing
/// store (JSON now, SQLite later) can change behind it without touching the journal. Portability —
/// export/import to a file — is a separate concern in <see cref="JsonCheckInArchive"/>, deliberately
/// kept off this seam so swapping the store stays a one-class change.
/// </summary>
public interface ICheckInRepository
{
    IReadOnlyList<CheckIn> LoadAll();

    void SaveAll(IReadOnlyList<CheckIn> checkIns);
}
