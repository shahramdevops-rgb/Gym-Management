using Gym.Application.Common;
using Gym.Infrastructure.Identity;

namespace Gym.Api.Common;

/// <summary>
/// <see cref="ICurrentUser"/> for code running inside an HTTP request: the <c>sub</c> claim of
/// the access token that the JWT bearer handler already validated.
/// </summary>
/// <remarks>
/// A singleton, because <see cref="IHttpContextAccessor"/> already resolves to the current
/// request on every call. That lets singletons such as the audit interceptor depend on it
/// without capturing one request's user for the lifetime of the application.
/// </remarks>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return Guid.TryParse(principal.FindFirst(JwtClaimNames.Subject)?.Value, out var userId)
                ? userId
                : null;
        }
    }

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
