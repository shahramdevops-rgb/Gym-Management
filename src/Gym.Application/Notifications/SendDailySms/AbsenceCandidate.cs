using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Notifications;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.SendDailySms;

/// <summary>
/// The absence SMS a run is to send today (BUSINESS_RULES.md §10 <i>The absence SMS</i>, task 10.12),
/// built but not yet saved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The board's members.</b> The query asks for the same plans as «دیگر نمی‌آید» on «نیاز به
/// اقدام» (§12): an active member's membership, not cancelled or frozen, in its dates, with sessions
/// left; and the days away come from <see cref="Absence"/>, as there. Only the number differs.
/// </para>
/// <para>
/// <b>The query narrows, <see cref="SmsAudience"/> decides</b>, as for the other kinds: each member's
/// last visit and the day their latest absence SMS counted from are read here, and the rule says
/// whether this absence has had one. The unique index on the member and that day is what really
/// keeps it from being paid for twice.
/// </para>
/// </remarks>
public sealed class AbsenceCandidate(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<List<Notification>> FindAsync(SmsSettings settings, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // The database holds "on means filled", and a run only starts for a kind that is on.
        var daysAway = settings.AbsenceDaysAway
            ?? throw new InvalidOperationException("The absence SMS is on with no number of days.");

        var rows = await (
                from plan in db.Subscriptions.AsNoTracking()
                join member in db.Members.AsNoTracking() on plan.MemberId equals member.Id
                where member.IsActive && !plan.IsSingleSession && plan.CancelledAt == null &&
                    plan.FrozenSince == null && plan.StartDate <= today && plan.EndDate >= today &&
                    plan.UsedSessions < plan.TotalSessions
                select new AbsenceRow
                {
                    Plan = plan,
                    FullName = member.FullName,
                    PhoneNumber = member.PhoneNumber,

                    // A cancelled check-in is not a visit; a cardio-only one is (§12).
                    LastVisitAt = db.Attendances
                        .Where(visit => visit.MemberId == member.Id && visit.CancelledAt == null)
                        .Max(visit => (DateTimeOffset?)visit.CheckedInAt),

                    // Whatever became of it: a failed one still counts as written (the Owner can resend it).
                    LastAbsentSince = db.Notifications
                        .Where(notification => notification.Kind == NotificationKind.Absence && notification.MemberId == member.Id)
                        .Max(notification => notification.AbsentSince),
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new
            {
                Row = row,
                LastVisitOn = row.LastVisitAt is { } moment ? calendar.DayOf(moment) : (DateOnly?)null,
            })
            .Where(row => SmsAudience.IsAbsenceDue(row.Row.Plan, row.LastVisitOn, row.Row.LastAbsentSince, today, daysAway))
            .Select(row => new
            {
                row.Row,
                Since = Absence.Since(row.LastVisitOn, row.Row.Plan.StartDate),
            })

            // The longest away first, as on the board. One plan is usable at a time (§4), but one
            // member is one message whatever the rows say.
            .OrderBy(row => row.Since)
            .ThenBy(row => row.Row.Plan.MemberId)
            .DistinctBy(row => row.Row.Plan.MemberId)
            .Select(row => Notification.ForAbsence(
                row.Row.Plan.MemberId,
                row.Since,
                row.Row.PhoneNumber,
                SmsText.ForAbsence(row.Row.FullName)))
            .ToList();
    }

    /// <summary>Set with an initializer, not a constructor, so EF Core can build it inside the query.</summary>
    private sealed class AbsenceRow
    {
        public required Subscription Plan { get; init; }

        public required string FullName { get; init; }

        public required string PhoneNumber { get; init; }

        public required DateTimeOffset? LastVisitAt { get; init; }

        public required DateOnly? LastAbsentSince { get; init; }
    }
}
