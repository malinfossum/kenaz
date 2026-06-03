using System.Security.Cryptography;
using System.Text;

namespace Kenaz.Api;

/// <summary>Wraps the configured token so the filter receives it by DI, not as a captured string.</summary>
public sealed record ApiToken(string Value);

/// <summary>
/// Group filter for /checkins: requires "Authorization: Bearer &lt;token&gt;" and compares the
/// presented token to the configured one in constant time. Any failure is a 401 — decided before
/// the route handler runs.
/// </summary>
public sealed class BearerTokenFilter : IEndpointFilter
{
    private readonly byte[] _expected;

    public BearerTokenFilter(ApiToken token)
    {
        _expected = Encoding.UTF8.GetBytes(token.Value);
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        const string prefix = "Bearer ";
        var header = context.HttpContext.Request.Headers.Authorization.ToString();

        if (header.StartsWith(prefix, StringComparison.Ordinal))
        {
            var presented = Encoding.UTF8.GetBytes(header.Substring(prefix.Length));
            if (CryptographicOperations.FixedTimeEquals(presented, _expected))
            {
                return await next(context);
            }
        }

        context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Unauthorized();
    }
}
