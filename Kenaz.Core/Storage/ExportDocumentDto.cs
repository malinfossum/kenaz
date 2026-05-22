namespace Kenaz.Core;

/// <summary>
/// Serialization shape for an export file: a versioned envelope around the check-ins.
/// The version lets a future import recognise and tolerate older files.
/// </summary>
internal sealed class ExportDocumentDto
{
    public int SchemaVersion { get; set; }
    public DateTimeOffset ExportedAt { get; set; }
    public List<CheckInDto> CheckIns { get; set; } = new();
}
