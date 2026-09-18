using Gym.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;

namespace Gym.Api.Authorization;

/// <summary>
/// The forced password change gate: met only when the access token does not say
/// <c>must_change_password=true</c> (BUSINESS_RULES.md §1).
/// </summary>
/// <remarks>
/// A requirement of its own, rather than an inline assertion, so that
/// <see cref="ProblemDetailsAuthorizationResultHandler"/> can see that <i>this</i> is what failed
/// and answer <c>Auth.PasswordChangeRequired</c>, which tells the frontend where to send the user.
/// </remarks>
public sealed class PasswordChangedRequirement : IAuthorizationRequirement;

/// <summary>Checks <see cref="PasswordChangedRequirement"/> against the token's claim.</summary>
public sealed class PasswordChangedHandler : AuthorizationHandler<PasswordChangedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PasswordChangedRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Every token this API issues carries the claim, so a missing claim is not "false":
        // it is a token that did not come from here, and it does not pass.
        var claim = context.User.FindFirst(JwtClaimNames.MustChangePassword)?.Value;

        if (string.Equals(claim, "false", StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
