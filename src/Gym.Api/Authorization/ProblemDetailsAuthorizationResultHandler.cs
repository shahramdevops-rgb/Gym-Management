using Gym.Api.Common;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Gym.Api.Authorization;

/// <summary>
/// Turns authorization failures into the same ProblemDetails shape as every other failure.
/// </summary>
/// <remarks>
/// Without this, a missing token gets an empty 401 and a wrong role an empty 403, and the
/// frontend has no <c>code</c> to map to a Persian message. The status codes themselves are
/// the framework's; this only adds the body.
/// </remarks>
public sealed class ProblemDetailsAuthorizationResultHandler(IProblemDetailsService problemDetails)
    : IAuthorizationMiddlewareResultHandler
{
    public const string UnauthenticatedCode = "Auth.Unauthenticated";
    public const string PasswordChangeRequiredCode = "Auth.PasswordChangeRequired";
    public const string ForbiddenCode = "Auth.Forbidden";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Challenged)
        {
            // Lets the JWT bearer handler set 401 and its WWW-Authenticate header first.
            await context.ChallengeAsync();
            await WriteAsync(context, StatusCodes.Status401Unauthorized, "Authentication required.",
                "A valid access token is required.", UnauthenticatedCode);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            // The gate is reported even when a role check failed as well: changing the password
            // is the only thing this user can do next, so that is what the frontend should show.
            var mustChangePassword = authorizeResult.AuthorizationFailure?.FailedRequirements
                .OfType<PasswordChangedRequirement>()
                .Any() == true;

            await WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden.",
                mustChangePassword
                    ? "The password must be changed before anything else."
                    : "The current user is not allowed to do this.",
                mustChangePassword ? PasswordChangeRequiredCode : ForbiddenCode);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }

    private Task WriteAsync(HttpContext context, int status, string title, string detail, string code)
    {
        context.Response.StatusCode = status;

        return problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { [ProblemDetailsFields.Code] = code },
            },
        }).AsTask();
    }
}
