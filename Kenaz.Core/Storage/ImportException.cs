namespace Kenaz.Core;

/// <summary>Raised when an export file can't be read at all (missing, unreadable, not JSON, or a newer schema).</summary>
public sealed class ImportException : Exception
{
    public ImportException(string message) : base(message)
    {
    }
}
