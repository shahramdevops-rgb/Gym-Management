using System.Linq.Expressions;

using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// What the two session tables share (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>): the
/// time columns, and the checks the database holds for every session as the Domain does.
/// </summary>
internal static class TimetableSessionColumns
{
    /// <summary>
    /// <c>start_time</c> and <c>end_time</c>, not <c>start</c> and <c>end</c>: <c>end</c> is a
    /// reserved word in SQL, and every check and query would have to quote it.
    /// </summary>
    public static void Map<TSession>(
        EntityTypeBuilder<TSession> builder,
        Expression<Func<TSession, TimeOnly>> start,
        Expression<Func<TSession, TimeOnly>> end)
        where TSession : class
    {
        builder.Property(start).HasColumnName("start_time");
        builder.Property(end).HasColumnName("end_time");
    }

    public static void AddChecks(TableBuilder table, string tableName)
    {
        table.HasCheckConstraint(
            $"ck_{tableName}_capacity",
            $"capacity BETWEEN {TimetableRules.MinCapacity} AND {TimetableRules.MaxCapacity}");

        // A standing note (15.7e) is kept trimmed, and a blank one is none.
        table.HasCheckConstraint($"ck_{tableName}_note_not_blank", NotBlank("note"));

        AddTimeChecks(table, tableName);
    }

    /// <summary>The standing note's column on a session table: none, or up to <see cref="SessionNoteText.MaxStandingLength"/> characters.</summary>
    public static void MapNote<TSession>(EntityTypeBuilder<TSession> builder, Expression<Func<TSession, string?>> note)
        where TSession : class =>
        builder.Property(note).HasMaxLength(SessionNoteText.MaxStandingLength);

    /// <summary>
    /// A note column is null or holds text with no space, tab or line break at either end, as the
    /// Domain keeps it (it trims every kind of white space, so it never writes what this refuses).
    /// </summary>
    public static string NotBlank(string column) =>
        $@"{column} IS NULL OR ({column} <> '' AND {column} = btrim({column}, E' \t\r\n'))";

    /// <summary>The time checks alone, for a table that names a session by its times (15.6b) and has no capacity.</summary>
    public static void AddTimeChecks(TableBuilder table, string tableName)
    {
        // The same day: no session past midnight.
        table.HasCheckConstraint($"ck_{tableName}_end_after_start", "end_time > start_time");

        // On the quarter hour, whole minutes. A time's "second" part holds its fractions too.
        foreach (var column in (string[])["start_time", "end_time"])
        {
            table.HasCheckConstraint(
                $"ck_{tableName}_{column}_on_step",
                $"extract(second FROM {column}) = 0 AND extract(minute FROM {column})::int % {TimetableRules.MinuteStep} = 0");
        }

        // From 07:00 to 22:00 (BUSINESS_RULES.md §15, the developer, 1405/08/13).
        table.HasCheckConstraint(
            $"ck_{tableName}_within_hours",
            "start_time >= TIME '07:00' AND end_time <= TIME '22:00'");
    }
}
