namespace Kenaz.Api;

/// <summary>
/// Serializes mutating requests so two overlapping read-modify-write journal calls can't lose
/// each other's data. A single-permit gate; GET handlers don't take it.
/// </summary>
public sealed class WriteLock
{
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

    public Task WaitAsync() => _gate.WaitAsync();

    public void Release() => _gate.Release();
}
