using System.Text.Json.Serialization;

namespace Gym.Application.Staff;

/// <summary>A staff account, or a shop admin's (§16), as the Owner's staff screen shows it.</summary>
/// <param name="IsLockedOut">
/// Locked after too many wrong passwords, from unknown devices or from a device the person uses.
/// Unlocking or resetting the password clears it.
/// </param>
public sealed record StaffResponse(
    Guid Id,
    string UserName,
    string FullName,
    bool IsActive,
    bool MustChangePassword,
    bool IsLockedOut,
    [property: JsonConverter(typeof(JsonStringEnumConverter<StaffRole>))] StaffRole Role);
