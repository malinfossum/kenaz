using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kenaz.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Kenaz.Tests;

public class InsightsApiTests
{
    private const string Token = "test-token-abc123";

    private string _dir = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-insights-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var dbPath = Path.Combine(_dir, "checkins.db");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kenaz:DbPath", dbPath);
            b.UseSetting("Kenaz:Token", Token);
        });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Test]
    public async Task Get_insights_without_token_returns_401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/insights");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.WwwAuthenticate.Single().Scheme, Is.EqualTo("Bearer"));
    }

    [Test]
    public async Task Get_insights_with_wrong_token_returns_401()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-the-token");

        var response = await _client.GetAsync("/insights");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.WwwAuthenticate.Single().Scheme, Is.EqualTo("Bearer"));
    }

    [Test]
    public async Task Get_insights_on_empty_store_returns_200_with_no_data_flags()
    {
        var insights = await _client.GetFromJsonAsync<InsightsResponse>("/insights");

        Assert.That(insights, Is.Not.Null);
        Assert.That(insights!.HasWeekData, Is.False);
        Assert.That(insights.MoodAverage, Is.Null);
        Assert.That(insights.HasHighlights, Is.False);
        Assert.That(insights.SleepPatternConfident, Is.False);
        Assert.That(insights.ShowSleepTeaser, Is.False);
    }

    [Test]
    public async Task Get_insights_with_seeded_data_returns_computed_values_and_flags()
    {
        // The endpoint summarizes over DateTimeOffset.Now, so seed dates relative to the real today.
        var today = DateOnly.FromDateTime(DateTime.Now);

        // 5 long-sleep days mood 8 + 5 short-sleep days mood 5: confident, gap 3 → teaser, more-sleep-better.
        for (var i = 0; i < 5; i++)
        {
            await _client.PutAsJsonAsync($"/checkins/{today.AddDays(-i):yyyy-MM-dd}",
                new UpsertCheckInRequest(Mood: 8, Energy: null, Sleep: 8m, Note: null));
        }
        for (var i = 5; i < 10; i++)
        {
            await _client.PutAsJsonAsync($"/checkins/{today.AddDays(-i):yyyy-MM-dd}",
                new UpsertCheckInRequest(Mood: 5, Energy: null, Sleep: 6m, Note: null));
        }

        var insights = await _client.GetFromJsonAsync<InsightsResponse>("/insights");

        Assert.That(insights!.HasWeekData, Is.True);
        Assert.That(insights.MoodAverage, Is.Not.Null);
        Assert.That(insights.HasHighlights, Is.True);
        Assert.That(insights.BrightestDay!.Date, Is.EqualTo($"{today:yyyy-MM-dd}"));
        Assert.That(insights.SleepPatternConfident, Is.True);
        Assert.That(insights.ShowSleepTeaser, Is.True);
        Assert.That(insights.TeaserDirection, Is.EqualTo("MoreSleepBetter"));
    }
}
