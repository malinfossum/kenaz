namespace Kenaz.Core;

/// <summary>
/// Thrown by <see cref="JsonToSqliteMigrator"/> when the migration cannot be completed
/// safely. Carries the original exception (if any) as <see cref="Exception.InnerException"/>
/// so the console caller can show a warm message without losing diagnostic detail.
/// </summary>
public class MigrationException : Exception
{
    public MigrationException(string message) : base(message) { }

    public MigrationException(string message, Exception innerException) : base(message, innerException) { }
}
