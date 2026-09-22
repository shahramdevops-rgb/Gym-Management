namespace Gym.Application.Members.UpdateMember;

/// <param name="BirthDate">Optional and usually absent (BUSINESS_RULES.md §2). Gregorian; the frontend converts.</param>
/// <param name="Version">The <c>version</c> from the member as it was read before editing.</param>
public sealed record UpdateMemberCommand(string FullName, string PhoneNumber, string? Notes, DateOnly? BirthDate, uint Version);
