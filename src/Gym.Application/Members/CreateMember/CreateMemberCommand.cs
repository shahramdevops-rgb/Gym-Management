namespace Gym.Application.Members.CreateMember;

/// <param name="PhoneNumber">As typed: any common format, Persian, Arabic or English digits.</param>
public sealed record CreateMemberCommand(string FullName, string PhoneNumber, string? Notes);
