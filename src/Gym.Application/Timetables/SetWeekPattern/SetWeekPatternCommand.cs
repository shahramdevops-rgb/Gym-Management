using System.Text.Json.Serialization;

namespace Gym.Application.Timetables.SetWeekPattern;

/// <summary>«ذخیره الگو»: the week's sessions, from <paramref name="StartsOn"/> on.</summary>
public sealed record SetWeekPatternCommand(DateOnly StartsOn, IReadOnlyList<WeeklySessionItem> Sessions);

public sealed record WeeklySessionItem(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek Day,
    TimeOnly Start,
    TimeOnly End,
    int Capacity);
