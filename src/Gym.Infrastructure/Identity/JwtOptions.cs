using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// The <c>Jwt</c> configuration section, shared by the code that issues tokens
/// (<see cref="JwtAccessTokenIssuer"/>) and the code that validates them (Gym.Api).
/// </summary>
/// <remarks>
/// Both sides build their keys and validation rules from this one class, so a token issued by
/// this API is by construction a token this API accepts. <see cref="SigningKey"/> is a secret:
/// locally it comes from user-secrets, in production from the <c>Jwt__SigningKey</c>
/// environment variable. <see cref="JwtOptionsValidator"/> stops startup when it is missing.
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC-SHA256 needs a key at least as long as its 256-bit output. Anything shorter is
    /// rejected by the token library at the first login, so the validator rejects it at startup.
    /// </summary>
    public const int MinimumSigningKeyBytes = 32;

    /// <summary>
    /// Fixed by BUSINESS_RULES.md §1, so it is a constant rather than a setting: a rule should
    /// not be changeable by editing a configuration file.
    /// </summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    /// <summary>
    /// The rules a presented token must pass. Used by Gym.Api's JWT bearer handler and by the
    /// tests, so both check exactly what production checks.
    /// </summary>
    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = CreateSigningKey(),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,

        // The default is five minutes, which would quietly turn a 15-minute token into a
        // 20-minute one. Skew exists for clocks on different machines; this API issues and
        // validates its own tokens on one machine, so there is no skew to allow for.
        ClockSkew = TimeSpan.Zero,

        // The claim names JwtAccessTokenIssuer writes, so User.Identity.Name and
        // User.IsInRole(...) work without any claim-type mapping.
        NameClaimType = JwtClaimNames.Name,
        RoleClaimType = JwtClaimNames.Role,
    };
}
