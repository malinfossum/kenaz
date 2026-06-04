using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kenaz.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Kenaz.Tests;

public class CheckInApiTests
{
    private const string Token = "test-token-abc123";

    private string _dir = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-api-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var dbPath = Path.Combine(_dir, "checkins.db");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kenaz:DbPath", dbPath);
            b.UseSetting("Kenaz:Token", Token);
        });

        _client = _factory.CreateClient();
        // Default to authorized; the 401 tests clear or override this.
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
        // Same Windows pool/file-lock lesson as the SQLite fixtures.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Test]
    public async Task Get_checkins_without_token_returns_401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/checkins");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_checkins_with_wrong_token_returns_401()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-the-token");

        var response = await _client.GetAsync("/checkins");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_checkins_on_empty_store_returns_200_and_empty_array()
    {
        var checkIns = await _client.GetFromJsonAsync<List<CheckInResponse>>("/checkins");

        Assert.That(checkIns, Is.Not.Null);
        Assert.That(checkIns, Is.Empty);
    }

    [Test]
    public async Task Put_creates_checkin_and_it_appears_in_history()
    {
        var put = await _client.PutAsJsonAsync("/checkins/2026-05-31",
            new UpsertCheckInRequest(Mood: 7, Energy: 6, Sleep: 7.5m, Note: "steady"));
        Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await put.Content.ReadFromJsonAsync<CheckInResponse>();
        Assert.That(body!.Date, Is.EqualTo("2026-05-31"));
        Assert.That(body.Mood, Is.EqualTo(7));

        var all = await _client.GetFromJsonAsync<List<CheckInResponse>>("/checkins");
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all![0].Sleep, Is.EqualTo(7.5m));
        Assert.That(all[0].Note, Is.EqualTo("steady"));
    }

    [Test]
    public async Task Put_twice_updates_in_place_and_preserves_CreatedAt()
    {
        var first = await (await _client.PutAsJsonAsync("/checkins/2026-05-31",
            new UpsertCheckInRequest(5, null, null, null))).Content.ReadFromJsonAsync<CheckInResponse>();
        var second = await (await _client.PutAsJsonAsync("/checkins/2026-05-31",
            new UpsertCheckInRequest(9, null, null, null))).Content.ReadFromJsonAsync<CheckInResponse>();

        var all = await _client.GetFromJsonAsync<List<CheckInResponse>>("/checkins");
        Assert.That(all, Has.Count.EqualTo(1), "Re-PUT updates in place, not a second row.");
        Assert.That(second!.Mood, Is.EqualTo(9));
        Assert.That(second.CreatedAt, Is.EqualTo(first!.CreatedAt), "CreatedAt is stable across updates.");
        Assert.That(second.UpdatedAt, Is.GreaterThanOrEqualTo(first.UpdatedAt));
    }

    [Test]
    public async Task Round_trips_decimal_sleep_and_null_fields()
    {
        await _client.PutAsJsonAsync("/checkins/2026-05-31",
            new UpsertCheckInRequest(Mood: null, Energy: null, Sleep: 7.5m, Note: null));

        var all = await _client.GetFromJsonAsync<List<CheckInResponse>>("/checkins");
        Assert.That(all![0].Sleep, Is.EqualTo(7.5m));
        Assert.That(all[0].Mood, Is.Null);
        Assert.That(all[0].Energy, Is.Null);
        Assert.That(all[0].Note, Is.Null);
    }

    [Test]
    public async Task Put_with_all_null_fields_returns_400()
    {
        var response = await _client.PutAsJsonAsync("/checkins/2026-05-31",
            new UpsertCheckInRequest(Mood: null, Energy: null, Sleep: null, Note: null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Put_with_out_of_range_mood_returns_400()
    {
        var response = await _client.PutAsJsonAsync("/checkins/2026-05-31",
            new UpsertCheckInRequest(Mood: 99, Energy: null, Sleep: null, Note: null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Put_with_invalid_date_format_returns_400()
    {
        var response = await _client.PutAsJsonAsync("/checkins/not-a-date",
            new UpsertCheckInRequest(Mood: 7, Energy: null, Sleep: null, Note: null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Put_with_null_json_body_returns_400()
    {
        // A literal `null` JSON body binds as a null UpsertCheckInRequest, so the handler's explicit
        // null guard returns 400. (We assert the `null`-body case directly: a truly empty body can
        // surface as 415 depending on content-type, which isn't the case we're pinning here.)
        var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PutAsync("/checkins/2026-05-31", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Get_unknown_date_returns_404()
    {
        var response = await _client.GetAsync("/checkins/2020-01-01");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Get_by_date_with_invalid_format_returns_400()
    {
        var response = await _client.GetAsync("/checkins/31-05-2026");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Get_existing_date_returns_200_with_the_checkin()
    {
        await _client.PutAsJsonAsync("/checkins/2026-05-31",
            new UpsertCheckInRequest(Mood: 8, Energy: null, Sleep: null, Note: null));

        var fetched = await _client.GetFromJsonAsync<CheckInResponse>("/checkins/2026-05-31");

        Assert.That(fetched, Is.Not.Null);
        Assert.That(fetched!.Date, Is.EqualTo("2026-05-31"));
        Assert.That(fetched.Mood, Is.EqualTo(8));
    }

    [Test]
    public async Task Get_history_orders_newest_first()
    {
        await _client.PutAsJsonAsync("/checkins/2026-05-29", new UpsertCheckInRequest(1, null, null, null));
        await _client.PutAsJsonAsync("/checkins/2026-05-31", new UpsertCheckInRequest(2, null, null, null));
        await _client.PutAsJsonAsync("/checkins/2026-05-30", new UpsertCheckInRequest(3, null, null, null));

        var all = await _client.GetFromJsonAsync<List<CheckInResponse>>("/checkins");

        Assert.That(all!.Select(c => c.Date),
            Is.EqualTo(new[] { "2026-05-31", "2026-05-30", "2026-05-29" }));
    }
}
