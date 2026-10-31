using System.Text.Json.Serialization;

namespace Gym.Application.Bookings.SetStudentSchedule;

/// <summary>«ذخیره برنامه»: the student comes to these weekly sessions from <paramref name="StartsOn"/> to <paramref name="EndsOn"/>.</summary>
public sealed record SetStudentScheduleCommand(DateOnly StartsOn, DateOnly EndsOn, IReadOnlyList<WeeklySlotItem> Slots);

public sealed record WeeklySlotItem(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek Day,
    TimeOnly Start,
    TimeOnly End);
