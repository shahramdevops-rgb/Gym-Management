namespace Gym.Application.Staff.CreateStaff;

/// <param name="TemporaryPassword">Typed by the Owner and told to the new staff member, who must change it at first login.</param>
public sealed record CreateStaffCommand(string UserName, string FullName, string TemporaryPassword);
