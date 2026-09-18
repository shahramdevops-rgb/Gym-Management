namespace Gym.Infrastructure.Identity;

/// <summary>
/// Claim names written into the access token. Short JWT-style names rather than the long
/// <c>http://schemas.xmlsoap.org/...</c> URIs .NET uses by default, because the frontend reads
/// the same token and a claim called <c>role</c> is what it expects.
/// </summary>
public static class JwtClaimNames
{
    /// <summary>The user's id. Standard JWT "subject" claim.</summary>
    public const string Subject = "sub";

    /// <summary>A unique id per token, so a single token can be identified in logs.</summary>
    public const string TokenId = "jti";

    public const string Name = "name";

    public const string Role = "role";

    public const string FullName = "full_name";

    /// <summary>Task 1.4's gate reads this to allow only change-password and logout.</summary>
    public const string MustChangePassword = "must_change_password";
}
