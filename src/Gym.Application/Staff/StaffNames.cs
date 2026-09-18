namespace Gym.Application.Staff;

/// <summary>Tidies a typed full name before it is validated and saved.</summary>
/// <remarks>
/// Trim and collapse repeated whitespace only. The full Persian normalization in
/// BUSINESS_RULES.md §13 is for search columns and arrives with member search in Phase 2.
/// </remarks>
public static class StaffNames
{
    public const int FullNameMaxLength = 200;

    public static string Clean(string? fullName) =>
        string.Join(' ', (fullName ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
