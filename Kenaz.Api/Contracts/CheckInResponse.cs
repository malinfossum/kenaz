using System.Globalization;
using Kenaz.Core;

namespace Kenaz.Api;

/// <summary>The wire shape of a check-in. Date is the yyyy-MM-dd key the routes use.</summary>
public record CheckInResponse(
    string Date,
    int? Mood,
    int? Energy,
    decimal? Sleep,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CheckInResponse From(CheckIn c) => new CheckInResponse(
        c.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        c.Mood, c.Energy, c.Sleep, c.Note, c.CreatedAt, c.UpdatedAt);
}
