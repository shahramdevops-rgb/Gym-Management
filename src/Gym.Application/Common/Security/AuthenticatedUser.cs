namespace Gym.Application.Common.Security;

/// <summary>
/// What the Application layer knows about a user who has just proved their password.
/// </summary>
/// <remarks>
/// A plain record instead of the Identity <c>User</c> class, which lives in Infrastructure
/// (ADR 0002). It carries exactly what the access token needs and nothing that could leak,
/// such as the password hash or security stamp.
/// </remarks>
public sealed record AuthenticatedUser(
    Guid Id,
    string UserName,
    string FullName,
    IReadOnlyList<string> Roles,
    bool MustChangePassword);
