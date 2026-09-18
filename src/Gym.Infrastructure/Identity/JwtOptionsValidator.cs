using System.Text;

using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// Stops startup when the <c>Jwt</c> section is incomplete, with a message that says how to
/// fix it. Without this the API would start normally and fail at the first login.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinimumSigningKeyBytes)
        {
            failures.Add(
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyBytes} bytes. Locally, run: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"<random 32+ characters>\" --project src/Gym.Api. " +
                "In production, supply Jwt__SigningKey.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
