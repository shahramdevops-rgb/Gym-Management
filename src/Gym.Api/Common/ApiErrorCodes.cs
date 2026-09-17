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

    /// <summary>Something unexpected broke. The body carries no detail beyond the correlation id.</summary>
    public const string Unexpected = "General.Unexpected";
}
