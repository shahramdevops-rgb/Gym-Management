using System.Text.Json.Serialization;

namespace Gym.Application.Staff.CreateStaff;

/// <param name="TemporaryPassword">Typed by the Owner and told to the new staff member, who must change it at first login.</param>
/// <param name="Role">A staff member or a shop admin (BUSINESS_RULES.md §16); a staff member when left out.</param>
public sealed record CreateStaffCommand(
    string UserName,
    string FullName,
    string TemporaryPassword,
    [property: JsonConverter(typeof(JsonStringEnumConverter<StaffRole>))] StaffRole Role = StaffRole.Staff);
