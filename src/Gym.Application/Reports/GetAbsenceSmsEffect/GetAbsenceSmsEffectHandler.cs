using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetAbsenceSmsEffect;

/// <summary>
/// Of the members the absence SMS went to in a range, how many came again within 14 days
/// (BUSINESS_RULES.md §12 <i>The absence SMS's effect</i>, task 10.19). Owner only; the endpoint's
/// policy says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>The query narrows, <see cref="AbsenceSmsEffect"/> decides</b>, as the SMS runs do: the
/// messages sent in the range are read with each member's first visit after the moment it was
/// sent, and the rule says which count and what became of each. Comparing moments, not days, keeps
/// a visit earlier on the same day, before the message, from being its answer.
/// </para>
/// <para>
/// No table of its own: everything is in <c>notifications</c> and <c>attendances</c>, so the
/// messages sent before this report was written are counted too.
/// </para>
/// </remarks>
public sealed class GetAbsenceSmsEffectHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<AbsenceSmsEffectResponse> Handle(GetAbsenceSmsEffectQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var from = query.From!.Value;
        var to = query.To!.Value;
        var start = calendar.StartOfDayUtc(from);
        var end = calendar.StartOfDayUtc(to.AddDays(1));
        var today = calendar.Today();

        var rows = await (
                from notification in db.Notifications.AsNoTracking()
                join member in db.Members.AsNoTracking() on notification.MemberId equals (Guid?)member.Id
                where notification.Kind == NotificationKind.Absence && notification.Status == NotificationStatus.Sent &&
                    notification.SentAt >= start && notification.SentAt < end
                select new
                {
                    notification.Id,
                    MemberId = member.Id,
                    member.FullName,
                    notification.Status,
                    notification.Delivery,
                    notification.CostRial,
                    notification.SentAt,

                    // A cancelled check-in is not a visit; a cardio-only one is (§10 The absence SMS).
                    FirstVisitAt = db.Attendances
                        .Where(visit => visit.MemberId == member.Id && visit.CancelledAt == null &&
                            visit.CheckedInAt > notification.SentAt)
                        .Min(visit => (DateTimeOffset?)visit.CheckedInAt),
                })
            .ToListAsync(cancellationToken);

        var messages = rows
            .Where(row => AbsenceSmsEffect.Counts(row.Status, row.Delivery))
            .Select(row =>
            {
                var sentOn = calendar.DayOf(row.SentAt!.Value);
                var firstVisitOn = row.FirstVisitAt is { } moment ? calendar.DayOf(moment) : (DateOnly?)null;
                var followUp = AbsenceSmsEffect.FollowUp(sentOn, firstVisitOn, today);

                return new
                {
                    row.CostRial,
                    Message = new AbsenceSmsMessageResponse(
                        row.Id,
                        row.MemberId,
                        row.FullName,
                        sentOn,
                        followUp.Outcome,
                        followUp.DaysToComeBack,
                        followUp.DaysLeft),
                };
            })
            .ToList();

        var list = messages
            .Select(item => item.Message)
            .OrderBy(message => message.Outcome)
            .ThenByDescending(message => message.SentOn)
            .ThenBy(message => message.FullName, StringComparer.Ordinal)
            .ToList();

        var cameBack = list.Where(message => message.Outcome == AbsenceSmsOutcome.CameBack).ToList();
        decimal? average = cameBack.Count == 0
            ? null
            : Math.Round((decimal)cameBack.Sum(message => message.DaysToComeBack!.Value) / cameBack.Count, 1, MidpointRounding.AwayFromZero);

        return new AbsenceSmsEffectResponse(
            from,
            to,
            list.Count,
            cameBack.Count,
            list.Count(message => message.Outcome == AbsenceSmsOutcome.Waiting),
            list.Count(message => message.Outcome == AbsenceSmsOutcome.DidNotComeBack),
            average,
            messages.Sum(item => item.CostRial ?? 0m) / 10m,
            list);
    }
}
