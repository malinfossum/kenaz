using Kenaz.Api;
using Kenaz.Core;

namespace Kenaz.Tests;

public class CheckInResponseTests
{
    [Test]
    public void From_maps_all_fields_and_formats_date_as_yyyy_MM_dd()
    {
        var created = new DateTimeOffset(2026, 5, 31, 8, 0, 0, TimeSpan.FromHours(2));
        var updated = created.AddHours(3);
        var checkIn = new CheckIn(new DateOnly(2026, 5, 31), mood: 7, energy: 6, sleep: 7.5m, note: "ok",
            createdAt: created, updatedAt: updated);

        var response = CheckInResponse.From(checkIn);

        Assert.That(response.Date, Is.EqualTo("2026-05-31"));
        Assert.That(response.Mood, Is.EqualTo(7));
        Assert.That(response.Energy, Is.EqualTo(6));
        Assert.That(response.Sleep, Is.EqualTo(7.5m));
        Assert.That(response.Note, Is.EqualTo("ok"));
        Assert.That(response.CreatedAt, Is.EqualTo(created));
        Assert.That(response.UpdatedAt, Is.EqualTo(updated));
    }
}
