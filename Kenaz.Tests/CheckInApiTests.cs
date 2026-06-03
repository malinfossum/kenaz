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
}
