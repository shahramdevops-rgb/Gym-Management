using Gym.Domain.Common;

namespace Gym.Application.Common.Security;

/// <summary>
/// Checks a user name and password, including lockout and the active flag.
/// </summary>
/// <remarks>
/// Declared here and implemented in Infrastructure with Identity's <c>UserManager</c>, because
/// Application cannot see the Identity <c>User</c> type (ADR 0002). Failures are the errors in
/// <c>AuthErrors</c>, chosen in the order BUSINESS_RULES.md §1 prescribes.
/// </remarks>
public interface IUserAuthenticator
{
    Task<Result<AuthenticatedUser>> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken);
}
