namespace Gym.Domain.Common;

/// <summary>
/// What kind of failure an <see cref="Error"/> describes.
/// </summary>
/// <remarks>
/// The Domain does not know about HTTP, so this enum names the <i>category</i> of failure and
/// Gym.Api owns the single table that turns a category into a status code (see
/// <c>ResultExtensions</c> and the table in docs/ARCHITECTURE.md). Keeping the two apart is
/// what lets the same handler be called from a background job, where 404 means nothing.
/// </remarks>
public enum ErrorType
{
    /// <summary>The request itself is malformed: a missing field, a value out of range.</summary>
    Validation = 0,

    /// <summary>The caller is not authenticated.</summary>
    Unauthorized = 1,

    /// <summary>The caller is authenticated but not allowed to do this.</summary>
    Forbidden = 2,

    /// <summary>The thing being acted on does not exist.</summary>
    NotFound = 3,

    /// <summary>The action collides with the current state: a duplicate, a concurrent edit.</summary>
    Conflict = 4,

    /// <summary>
    /// The request is well formed and the caller is allowed, but a rule in
    /// docs/BUSINESS_RULES.md says no — for example checking in with an expired subscription.
    /// </summary>
    BusinessRule = 5,
}
