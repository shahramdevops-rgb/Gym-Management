using System.Text;

using Gym.Infrastructure.Identity;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gym.Infrastructure.SiteAuth;

/// <summary>
/// The <c>SiteAuth</c> configuration section: the site's sign-in (BUSINESS_RULES.md §15, ADR 0010),
/// apart from the staff's <c>Jwt</c> section in everything. The rules themselves (the code's length,
/// lifetime, tries and limits, the session's 60 days) are constants, not settings.
/// </summary>
public sealed class SiteAuthOptions
{
    public const string SectionName = "SiteAuth";

    /// <summary>As long as HMAC-SHA256's output, like the staff's JWT key.</summary>
    public const int MinimumSecretBytes = 32;

    /// <summary>The same 15 minutes as the staff's (§1); the refresh cookie keeps the session going.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>BUSINESS_RULES.md §15: a new phone has 15 minutes after its code to sign up.</summary>
    public static readonly TimeSpan SignUpTicketLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The key the one-time codes are computed with (<see cref="HmacSignInCodes"/>). A secret:
    /// user-secrets locally, <c>SiteAuth__CodeSecret</c> on the server, never in git or in the
    /// database. Changing it makes every code already sent stop working, and nothing else.
    /// </summary>
    public string CodeSecret { get; set; } = string.Empty;

    /// <summary>Written into every site token and checked on every request. Not a secret.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The site app. Different from the staff's, so neither kind of token passes for the other.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// For sign-up tickets: another audience, so a ticket can never be used as an access token.
    /// Not a setting: it only has to differ from <see cref="Audience"/>.
    /// </summary>
    public string SignUpAudience => $"{Audience}-sign-up";

    /// <summary>
    /// Signs the site's tokens. A secret, different from the staff's <c>Jwt:SigningKey</c>:
    /// user-secrets locally, <c>SiteAuth__SigningKey</c> on the server. Changing it signs every
    /// member out of the site, and nobody out of the panel.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    /// <summary>The rules a site access token must pass; the bearer handler and the tests use the same.</summary>
    public TokenValidationParameters CreateValidationParameters() => CreateValidationParameters(Audience);

    /// <summary>The same rules for another audience: the sign-up ticket's.</summary>
    public TokenValidationParameters CreateValidationParameters(string audience) => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = audience,
        IssuerSigningKey = CreateSigningKey(),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,

        // One machine issues and checks these, as with the staff's (JwtOptions).
        ClockSkew = TimeSpan.Zero,
    };
}

/// <summary>
/// Stops startup without the site's secrets, or with a key, issuer or audience shared with the
/// staff's: the two sign-ins must not be able to accept each other's tokens (ADR 0010).
/// </summary>
public sealed class SiteAuthOptionsValidator(IOptions<JwtOptions> staff) : IValidateOptions<SiteAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, SiteAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (Encoding.UTF8.GetByteCount(options.CodeSecret) < SiteAuthOptions.MinimumSecretBytes)
        {
            failures.Add(MissingSecret("SiteAuth:CodeSecret"));
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < SiteAuthOptions.MinimumSecretBytes)
        {
            failures.Add(MissingSecret("SiteAuth:SigningKey"));
        }
        else if (options.SigningKey == staff.Value.SigningKey)
        {
            failures.Add("SiteAuth:SigningKey must differ from Jwt:SigningKey: the site's tokens must never be signed like the staff's.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || options.Issuer == staff.Value.Issuer)
        {
            failures.Add("SiteAuth:Issuer is required, and must differ from Jwt:Issuer.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience) || options.Audience == staff.Value.Audience)
        {
            failures.Add("SiteAuth:Audience is required, and must differ from Jwt:Audience.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string MissingSecret(string key) =>
        $"{key} must be at least {SiteAuthOptions.MinimumSecretBytes} bytes. Locally, run: " +
        $"dotnet user-secrets set \"{key}\" \"<random 32+ characters>\" --project src/Gym.Api. " +
        $"In production, supply {key.Replace(":", "__", StringComparison.Ordinal)}.";
}
