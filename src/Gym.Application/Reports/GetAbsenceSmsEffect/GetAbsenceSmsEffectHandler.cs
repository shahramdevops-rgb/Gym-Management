using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetAbsenceSmsEffect;

/// <summary>
/// «اثر پیامک غیبت» in «نیاز به اقدام»: every absence SMS since the first and what became of each,
/// whatever the dashboard's range, so a new month never empties it (BUSINESS_RULES.md §12 <i>The
/// absence SMS's effect</i>, task 10.19). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>The query narrows, <see cref="AbsenceSmsEffect"/> decides</b>, as the SMS runs do: the
/// messages are read with each member's first visit after the moment it was sent, and the member's
/// plans; the rule says which count and what became of each. Comparing moments, not days, keeps a
/// visit earlier on the same day, before the message, from being its answer.
/// </para>
/// <para>
/// No table of its own: everything is in <c>notifications</c>, <c>attendances</c> and
/// <c>subscriptions</c>, so the messages sent before this report was written are counted too. A gym
/// sends a few absence SMS a day, so every message is read each time.
/// </para>
/// </remarks>
public sealed class GetAbsenceSmsEffectHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<AbsenceSmsEffectResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.Today();

        var rows = await (
                from notification in db.Notifications.AsNoTracking()
                join member in db.Members.AsNoTracking() on notification.MemberId equals (Guid?)member.Id
                where notification.Kind == NotificationKind.Absence && notification.Status == NotificationStatus.Sent
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

        var counted = rows.Where(row => AbsenceSmsEffect.Counts(row.Status, row.Delivery)).ToList();

        var memberIds = counted.Select(row => row.MemberId).Distinct().ToList();
        var plans = await db.Subscriptions
            .AsNoTracking()
            .Where(plan => !plan.IsSingleSession && plan.CancelledAt == null && memberIds.Contains(plan.MemberId))
            .Select(plan => new { plan.MemberId, plan.StartDate, plan.EndDate, plan.FrozenSince })
            .ToListAsync(cancellationToken);
        var plansByMember = plans.ToLookup(plan => plan.MemberId);

        var messages = counted
            .Select(row =>
            {
                var sentOn = calendar.DayOf(row.SentAt!.Value);
                var firstVisitOn = row.FirstVisitAt is { } moment ? calendar.DayOf(moment) : (DateOnly?)null;

                // The plan the member held that day; one at a time (§4), the latest end if not.
                var plan = plansByMember[row.MemberId]
                    .Where(candidate => candidate.StartDate <= sentOn && candidate.EndDate >= sentOn)
                    .MaxBy(candidate => candidate.EndDate);
                var followUp = AbsenceSmsEffect.FollowUp(sentOn, plan?.EndDate, plan?.FrozenSince, firstVisitOn, today);

                return new AbsenceSmsMessageResponse(
                    row.Id,
                    row.MemberId,
                    row.FullName,
                    sentOn,
                    followUp.Outcome,
                    followUp.DaysToComeBack,
                    followUp.DaysLeft);
            })
            .OrderBy(message => message.Outcome)
            .ThenByDescending(message => message.SentOn)
            .ThenBy(message => message.FullName, StringComparer.Ordinal)
            .ToList();

        var cameBack = messages.Where(message => message.Outcome == AbsenceSmsOutcome.CameBack).ToList();
        decimal? average = cameBack.Count == 0
            ? null
            : Math.Round((decimal)cameBack.Sum(message => message.DaysToComeBack!.Value) / cameBack.Count, 1, MidpointRounding.AwayFromZero);
        var cameBackByDay = Enumerable.Range(0, AbsenceSmsEffect.WindowDays + 1)
            .Select(day => cameBack.Count(message => message.DaysToComeBack == day))
            .ToList();
        var costToman = counted.Sum(row => row.CostRial ?? 0m) / 10m;
        decimal? costPerReturn = cameBack.Count == 0
            ? null
            : Math.Round(costToman / cameBack.Count, 0, MidpointRounding.AwayFromZero);

        return new AbsenceSmsEffectResponse(
            messages.Count == 0 ? today : messages.Min(message => message.SentOn),
            today,
            messages.Count,
            cameBack.Count,
            messages.Count(message => message.Outcome == AbsenceSmsOutcome.Waiting),
            messages.Count(message => message.Outcome == AbsenceSmsOutcome.DidNotComeBack),
            messages.Count(message => message.Outcome == AbsenceSmsOutcome.NotMeasurable),
            average,
            cameBackByDay,
            costToman,
            costPerReturn,
            messages);
    }
}
