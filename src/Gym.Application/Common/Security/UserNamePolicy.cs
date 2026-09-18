namespace Gym.Application.Common.Security;

/// <summary>
/// BUSINESS_RULES.md §1: 3 to 50 characters from Identity's default set. Shared by the
/// validator and by Identity's options, so the two cannot disagree.
/// </summary>
public static class UserNamePolicy
{
    public const int MinimumLength = 3;

    public const int MaximumLength = 50;

    /// <summary>Identity's default set: Latin letters, digits and <c>- . _ @ +</c>.</summary>
    public const string AllowedCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public static bool HasOnlyAllowedCharacters(string? userName) =>
        userName is not null && userName.All(AllowedCharacters.Contains);
}
