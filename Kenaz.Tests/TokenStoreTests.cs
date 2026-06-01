using Kenaz.Api;

namespace Kenaz.Tests;

public class TokenStoreTests
{
    private string _dir = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-token-tests-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "api-token");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Test]
    public void GetOrCreate_generates_persists_and_is_stable()
    {
        var first = TokenStore.GetOrCreate(_path);

        Assert.That(first, Is.Not.Empty);
        Assert.That(File.Exists(_path), Is.True);
        // A second call reads the same persisted value rather than regenerating.
        Assert.That(TokenStore.GetOrCreate(_path), Is.EqualTo(first));
    }

    [Test]
    public void GetOrCreate_reads_existing_trimmed()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "  known-token\n");

        Assert.That(TokenStore.GetOrCreate(_path), Is.EqualTo("known-token"));
    }
}
