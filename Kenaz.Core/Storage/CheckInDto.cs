namespace Kenaz.Core;

/// <summary>
/// Serialization shape for a check-in. Plain public setters so System.Text.Json can read and
/// write it; the repository maps it to and from <see cref="CheckIn"/>, keeping serialization
/// concerns out of the domain model. Never trusted directly — every loaded record is re-validated
/// by constructing a real <see cref="CheckIn"/>.
/// </summary>
internal sealed class CheckInDto
{
    public DateOnly Date { get; set; }
    public int? Mood { get; set; }
    public int? Energy { get; set; }
    public decimal? Sleep { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
