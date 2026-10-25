using System.Text;

using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.SiteAuth;

/// <summary>
/// The <c>SiteAuth</c> configuration section: the secrets of the site's sign-in (BUSINESS_RULES.md
/// §15, ADR 0010), apart from the staff's <c>Jwt</c> section. The rules themselves (the code's
/// length, lifetime, tries and limits) are constants on <c>SignInCode</c>, not settings.
/// </summary>
public sealed class SiteAuthOptions
{
    public const string SectionName = "SiteAuth";

    /// <summary>As long as HMAC-SHA256's output, like the JWT signing key.</summary>
    public const int MinimumCodeSecretBytes = 32;

    /// <summary>
    /// The key the one-time codes are computed with (<see cref="HmacSignInCodes"/>). A secret:
    /// user-secrets locally, <c>SiteAuth__CodeSecret</c> on the server, never in git or in the
    /// database. Changing it makes every code already sent stop working, and nothing else.
    /// </summary>
    public string CodeSecret { get; set; } = string.Empty;
}

/// <summary>Stops startup without the secret, rather than failing at the first code.</summary>
public sealed class SiteAuthOptionsValidator : IValidateOptions<SiteAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, SiteAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Encoding.UTF8.GetByteCount(options.CodeSecret) >= SiteAuthOptions.MinimumCodeSecretBytes
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"SiteAuth:CodeSecret must be at least {SiteAuthOptions.MinimumCodeSecretBytes} bytes. Locally, run: " +
                "dotnet user-secrets set \"SiteAuth:CodeSecret\" \"<random 32+ characters>\" --project src/Gym.Api. " +
                "In production, supply SiteAuth__CodeSecret.");
    }
}
