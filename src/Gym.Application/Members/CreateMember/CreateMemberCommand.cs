namespace Gym.Application.Members.CreateMember;

/// <param name="PhoneNumber">As typed: any common format, Persian, Arabic or English digits.</param>
/// <param name="BirthDate">Optional and usually absent (BUSINESS_RULES.md §2). Gregorian; the frontend converts.</param>
public sealed record CreateMemberCommand(string FullName, string PhoneNumber, string? Notes, DateOnly? BirthDate);
