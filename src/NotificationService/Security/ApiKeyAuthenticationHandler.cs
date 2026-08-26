using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NotificationService.Security;

public class ApiKeyOptions
{
    public const string SectionName = "Security";
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public string ApiKeys { get; set; } = string.Empty;
}

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApiKeyOptions _apiKeyOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiKeyOptions> apiKeyOptions)
        : base(options, logger, encoder)
    {
        _apiKeyOptions = apiKeyOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var keyValues))
            return Task.FromResult(AuthenticateResult.Fail($"Missing {ApiKeyOptions.HeaderName} header."));

        var provided = keyValues.ToString();
        var validKeys = (_apiKeyOptions.ApiKeys ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (validKeys.Length == 0)
        {
            Logger.LogError("Security:ApiKeys is not configured — rejecting all requests");
            return Task.FromResult(AuthenticateResult.Fail("API keys not configured on server."));
        }

        if (!validKeys.Any(k => CryptographicEquals(k, provided)))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new[] { new Claim(ClaimTypes.Name, "api-client"), new Claim("auth_type", "api_key") };
        var identity = new ClaimsIdentity(claims, ApiKeyOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyOptions.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }
}
