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

        AddTimeChecks(table, tableName);
    }

    /// <summary>The time checks alone, for a table that names a session by its times (15.6b) and has no capacity.</summary>
    public static void AddTimeChecks(TableBuilder table, string tableName)
    {
        // The same day: no session past midnight.
        table.HasCheckConstraint($"ck_{tableName}_end_after_start", "end_time > start_time");

        // On 5-minute steps, whole minutes. A time's "second" part holds its fractions too.
        foreach (var column in (string[])["start_time", "end_time"])
        {
            table.HasCheckConstraint(
                $"ck_{tableName}_{column}_on_step",
                $"extract(second FROM {column}) = 0 AND extract(minute FROM {column})::int % {TimetableRules.MinuteStep} = 0");
        }
    }
}
