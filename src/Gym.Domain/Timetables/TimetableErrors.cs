using Gym.Domain.Common;

namespace Gym.Domain.Timetables;

public static class TimetableErrors
{
    public static readonly Error CapacityOutOfRange = Error.Validation(
        "Timetable.CapacityOutOfRange",
        $"A session's capacity is {TimetableRules.MinCapacity} to {TimetableRules.MaxCapacity}.");

    /// <summary>Times are whole minutes on <see cref="TimetableRules.MinuteStep"/>-minute steps.</summary>
    public static readonly Error TimeNotOnStep = Error.Validation(
        "Timetable.TimeNotOnStep",
        $"A session's times are on {TimetableRules.MinuteStep}-minute steps.");

    /// <summary>A session ends after it starts, the same day: none goes past midnight.</summary>
    public static readonly Error EndNotAfterStart = Error.Validation(
        "Timetable.EndNotAfterStart",
        "A session ends after it starts, on the same day.");

    public static readonly Error DuplicateSession = Error.Validation(
        "Timetable.DuplicateSession",
        "Two sessions of one day have the same start and end.");

    public static readonly Error TooManySessions = Error.Validation(
        "Timetable.TooManySessions",
        $"A day holds {TimetableRules.MaxSessionsPerDay} sessions at most.");

    public static readonly Error InvalidDay = Error.Validation(
        "Timetable.InvalidDay",
        "The day of the week is not one of the seven.");

    /// <summary>A pattern starts tomorrow at the earliest, and within the timetable's 12 weeks.</summary>
    public static readonly Error PatternStartOutOfRange = Error.BusinessRule(
        "Timetable.PatternStartOutOfRange",
        "A new pattern starts from tomorrow to the timetable's last day.");

    /// <summary>A date changed on its own is today or later, within the timetable's 12 weeks.</summary>
    public static readonly Error DayOutOfRange = Error.BusinessRule(
        "Timetable.DayOutOfRange",
        "Only a day from today to the timetable's last day can be changed.");

    /// <summary>A session of today that has started stays as it was: its time and capacity, and that it is there.</summary>
    public static readonly Error SessionStarted = Error.BusinessRule(
        "Timetable.SessionStarted",
        "A session that has already started cannot be changed or removed.");

    public static readonly Error SessionInThePast = Error.BusinessRule(
        "Timetable.SessionInThePast",
        "A new session cannot start in the past.");
}
