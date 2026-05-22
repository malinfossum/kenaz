using Kenaz.Core;

namespace Kenaz.Tests;

/// <summary>
/// Test double for <see cref="ICheckInRepository"/>. Holds check-ins in memory and copies
/// on the way in and out, so the journal can be tested with no file IO.
/// </summary>
public class InMemoryCheckInRepository : ICheckInRepository
{
    private List<CheckIn> _checkIns = new List<CheckIn>();

    public IReadOnlyList<CheckIn> LoadAll()
    {
        return _checkIns.ToList();
    }

    public void SaveAll(IReadOnlyList<CheckIn> checkIns)
    {
        _checkIns = checkIns.ToList();
    }
}
