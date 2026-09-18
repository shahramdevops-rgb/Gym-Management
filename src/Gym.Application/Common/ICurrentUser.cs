namespace Gym.Application.Common;

/// <summary>
/// Who is making the current request, for code that must not know about HTTP.
/// </summary>
/// <remarks>
/// Implemented in Gym.Api from the access token's <c>sub</c> claim. Application and
/// Infrastructure ask this interface instead of reading <c>HttpContext</c>, so the same handler
/// works from an endpoint, a test or a background job.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>
    /// The authenticated user's id, or null when nobody is logged in: anonymous endpoints,
    /// startup seeding and background jobs. Null is a real answer, not an error; it is what
    /// the audit fields record for work done on nobody's behalf.
    /// </summary>
    Guid? UserId { get; }
}
