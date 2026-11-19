using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.SendDailySms;

/// <summary>
/// The coaching period's SMS a run is to send today (BUSINESS_RULES.md §15 <i>The coaching period</i>,
/// <i>The period's last days</i>, task 15.7n), built but not yet saved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The query narrows, <see cref="SmsAudience"/> decides</b>, as for the other kinds: an open link
/// of an active member, its last period (the one that ends latest) ending within the window, and no
/// SMS yet for that link and last day. The rule then says whether the period is long enough and
/// gives the real days left.
/// </para>
/// <para>
/// <b>Once per last day.</b> A «تمدید» or a «ویرایش» that moves the last day makes a new pair, which
/// earns its own SMS when it comes near; the unique index on the link and the last day is what
/// really keeps one from being paid for twice.
/// </para>
/// </remarks>
public sealed class CoachingPeriodEndingCandidate(IAppDbContext db)
{
    public async Task<List<Notification>> FindAsync(SmsSettings settings, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var daysBefore = settings.CoachingPeriodEndingDays;

        // Days left count the last day as one, so N days left is a last day N − 1 days from today.
        var dueBy = today.AddDays(daysBefore - 1);

        var rows = await (
                from link in db.TrainerLinks.AsNoTracking()
                join member in db.Members.AsNoTracking() on link.MemberId equals member.Id
                join trainer in db.Trainers.AsNoTracking() on link.TrainerId equals trainer.Id
                let lastPeriod = db.CoachingPeriods
                    .Where(period => period.LinkId == link.Id)
                    .OrderByDescending(period => period.EndsOn)
                    .FirstOrDefault()
                where link.EndedOn == null && member.IsActive && lastPeriod != null &&
                    lastPeriod.EndsOn >= today && lastPeriod.EndsOn <= dueBy &&

                    // Whatever became of it: a failed one still counts as written (the Owner can resend it).
                    !db.Notifications.Any(notification =>
                        notification.Kind == NotificationKind.CoachingPeriodEnding &&
                        notification.TrainerLinkId == link.Id &&
                        notification.PeriodEndsOn == lastPeriod.EndsOn)
                orderby lastPeriod.EndsOn, member.FullName, link.Id
                select new PeriodRow
                {
                    LinkId = link.Id,
                    MemberId = member.Id,
                    FullName = member.FullName,
                    PhoneNumber = member.PhoneNumber,
                    TrainerFullName = trainer.FullName,
                    LastPeriod = lastPeriod,
                })
            .ToListAsync(cancellationToken);

        var notifications = new List<Notification>();
        foreach (var row in rows)
        {
            if (SmsAudience.CoachingDaysLeftToText(row.LastPeriod, today, daysBefore) is not { } daysLeft)
            {
                continue;
            }

            notifications.Add(Notification.ForCoachingPeriodEnding(
                row.MemberId,
                row.LinkId,
                row.LastPeriod.EndsOn,
                row.PhoneNumber,
                SmsText.ForCoachingPeriodEnding(row.FullName, row.TrainerFullName, daysLeft)));
        }

        return notifications;
    }

    /// <summary>Set with an initializer, not a constructor, so EF Core can build it inside the query.</summary>
    private sealed class PeriodRow
    {
        public required Guid LinkId { get; init; }

        public required Guid MemberId { get; init; }

        public required string FullName { get; init; }

        public required string PhoneNumber { get; init; }

        public required string TrainerFullName { get; init; }

        public required CoachingPeriod LastPeriod { get; init; }
    }
}
