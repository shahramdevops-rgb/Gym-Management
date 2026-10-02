namespace Gym.Application.Members.CreateMember;

/// <param name="PhoneNumber">As typed: any common format, Persian, Arabic or English digits.</param>
/// <param name="BirthDate">
/// Required (BUSINESS_RULES.md §2). Nullable only so a missing value is refused as
/// <c>Members.BirthDateRequired</c> instead of arriving as 0001-01-01. Gregorian; the frontend converts.
/// </param>
public sealed record CreateMemberCommand(string FullName, string PhoneNumber, string? Notes, DateOnly? BirthDate);
