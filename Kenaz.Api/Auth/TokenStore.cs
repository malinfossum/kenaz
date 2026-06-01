using System.Buffers.Text;
using System.Security.Cryptography;

namespace Kenaz.Api;

/// <summary>
/// Reads or generates the persisted bearer token. Stable across runs so the PWA and curl
/// sessions can rely on one value. Plaintext at rest in the same single-user folder as the
/// database (accepted invariant #9).
/// </summary>
public static class TokenStore
{
    public static string DefaultTokenPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Kenaz", "api-token");
    }

    public static string GetOrCreate(string path)
    {
        if (File.Exists(path))
        {
            return File.ReadAllText(path).Trim();
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, token);
        return token;
    }
}
