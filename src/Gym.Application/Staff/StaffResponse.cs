namespace Gym.Application.Staff;

/// <summary>A staff account as the Owner's staff screen shows it.</summary>
/// <param name="IsLockedOut">Locked after too many wrong passwords. Resetting the password unlocks it.</param>
public sealed record StaffResponse(
    Guid Id,
    string UserName,
    string FullName,
    bool IsActive,
    bool MustChangePassword,
    bool IsLockedOut);
