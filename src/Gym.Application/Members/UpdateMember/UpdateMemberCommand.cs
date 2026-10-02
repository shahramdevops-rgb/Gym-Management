namespace Gym.Application.Members.UpdateMember;

/// <param name="BirthDate">
/// Required (BUSINESS_RULES.md §2). Nullable only so a missing value is refused as
/// <c>Members.BirthDateRequired</c> instead of arriving as 0001-01-01. Gregorian; the frontend converts.
/// </param>
/// <param name="Version">The <c>version</c> from the member as it was read before editing.</param>
public sealed record UpdateMemberCommand(string FullName, string PhoneNumber, string? Notes, DateOnly? BirthDate, uint Version);
