namespace Gym.Api.Common;

/// <summary>
/// Error codes raised by the API itself rather than by a feature's domain rules.
/// </summary>
/// <remarks>
/// Feature codes live with their feature (<c>Members.PhoneAlreadyExists</c>); these few are
/// prefixed <c>General.</c> because they can come back from any endpoint.
/// </remarks>
public static class ApiErrorCodes
{
    /// <summary>The request failed validation; the <c>errors</c> field says which fields.</summary>
    public const string ValidationFailed = "General.ValidationFailed";

    /// <summary>The rate limiter refused the request (429). <c>Retry-After</c> says when to try again.</summary>
    public const string TooManyRequests = "General.TooManyRequests";

    /// <summary>Something unexpected broke. The body carries no detail beyond the correlation id.</summary>
    public const string Unexpected = "General.Unexpected";

    /// <summary>
    /// The address carried an encoded slash or backslash in its path (400). No page of the
    /// frontend ever sends one: see <see cref="Middleware.EncodedSlashMiddleware"/>.
    /// </summary>
    public const string InvalidPath = "General.InvalidPath";
}
