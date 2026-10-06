using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.SendDailySms;

/// <summary>
/// The messages a run of one kind is to send today (BUSINESS_RULES.md §10 <i>The four kinds</i>), built
/// but not yet saved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The query narrows, <see cref="SmsAudience"/> decides.</b> Each query asks the database for the
/// likely rows, with the conditions that need other rows (the member is active, has not renewed, has
/// not had this message); the domain rule then has the last word on each one.
/// </para>
/// <para>
/// <b>Who already has one is left out here, and refused again by the database.</b> The unique indexes
/// are what really keep a message from being paid for twice; leaving those rows out of the query
/// only saves the run from trying.
/// </para>
/// </remarks>
internal static class SmsCandidates
{
    public static Task<List<Notification>> FindAsync(
        IAppDbContext db, NotificationKind kind, SmsSettings settings, DateOnly today, CancellationToken cancellationToken)
    {
        var kindSettings = settings.For(kind);

        // The database holds "on means filled" (task 10.2), and a run only starts for a kind that is on.
        var threshold = kindSettings.Threshold
            ?? throw new InvalidOperationException($"SMS kind {kind} is on with no number.");
        var template = kindSettings.TemplateName
            ?? throw new InvalidOperationException($"SMS kind {kind} is on with no template.");

        return kind switch
        {
            NotificationKind.SubscriptionExpiring => RunningOutAsync(db, today, threshold, template, cancellationToken),
            NotificationKind.LowSessions => FewSessionsLeftAsync(db, today, threshold, template, cancellationToken),
            NotificationKind.Birthday => BirthdaysAsync(db, today, threshold, template, cancellationToken),
            NotificationKind.PayableDue => PayablesComingDueAsync(
                db,
                today,
                threshold,
                template,
                settings.OwnerPhone ?? throw new InvalidOperationException("Cheque reminders are on with no Owner number."),
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown notification kind."),
        };
    }

    private static async Task<List<Notification>> RunningOutAsync(
        IAppDbContext db, DateOnly today, int daysBefore, string template, CancellationToken cancellationToken)
    {
        var dueBy = today.AddDays(daysBefore);

        var rows = await ActivePlansOfActiveMembers(db, today, NotificationKind.SubscriptionExpiring)
            .Where(row => row.Subscription.EndDate <= dueBy)
            .OrderBy(row => row.Subscription.EndDate)
            .ThenBy(row => row.Subscription.Id)
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => SmsAudience.IsRunningOut(row.Subscription, today, daysBefore))
            .Select(row => Notification.ForSubscriptionExpiring(
                row.Subscription.MemberId,
                row.Subscription.Id,
                row.PhoneNumber,
                template,
                SmsValues.ForRunningOut(row.FullName, row.Subscription.EndDate)))
            .ToList();
    }

    private static async Task<List<Notification>> FewSessionsLeftAsync(
        IAppDbContext db, DateOnly today, int threshold, string template, CancellationToken cancellationToken)
    {
        var rows = await ActivePlansOfActiveMembers(db, today, NotificationKind.LowSessions)
            .Where(row => row.Subscription.TotalSessions - row.Subscription.UsedSessions <= threshold)
            .OrderBy(row => row.Subscription.EndDate)
            .ThenBy(row => row.Subscription.Id)
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => SmsAudience.HasFewSessionsLeft(row.Subscription, today, threshold))
            .Select(row => Notification.ForLowSessions(
                row.Subscription.MemberId,
                row.Subscription.Id,
                row.PhoneNumber,
                template,
                SmsValues.ForFewSessionsLeft(row.FullName, row.Subscription.RemainingSessions)))
            .ToList();
    }

    /// <summary>
    /// Every member with a birth date, active or not, with a subscription or without (§10: the one
    /// exception to "deactivated members receive no SMS"). Read whole: the Jalali month and day cannot
    /// be asked of a Gregorian column, and a gym's members fit in memory.
    /// </summary>
    private static async Task<List<Notification>> BirthdaysAsync(
        IAppDbContext db, DateOnly today, int daysBefore, string template, CancellationToken cancellationToken)
    {
        var (thisYear, _, _) = JalaliCalendar.Parts(today);

        var members = await db.Members
            .AsNoTracking()
            .OrderBy(member => member.FullName)
            .ThenBy(member => member.Id)
            .Select(member => new { member.Id, member.FullName, member.PhoneNumber, member.BirthDate })
            .ToListAsync(cancellationToken);

        // A greeting sent in Esfand may be for next year's birthday, so this year's and later count.
        var greeted = (await db.Notifications
                .AsNoTracking()
                .Where(notification => notification.Kind == NotificationKind.Birthday && notification.JalaliYear >= thisYear)
                .Select(notification => new { notification.MemberId, notification.JalaliYear })
                .ToListAsync(cancellationToken))
            .Select(notification => (notification.MemberId, notification.JalaliYear))
            .ToHashSet();

        var notifications = new List<Notification>();
        foreach (var member in members)
        {
            if (SmsAudience.BirthdayToGreet(member.BirthDate, today, daysBefore) is not { } birthday)
            {
                continue;
            }

            var (year, _, _) = JalaliCalendar.Parts(birthday);
            if (greeted.Contains((member.Id, year)))
            {
                continue;
            }

            notifications.Add(Notification.ForBirthday(
                member.Id, year, member.PhoneNumber, template, SmsValues.ForBirthday(member.FullName, birthday)));
        }

        return notifications;
    }

    /// <summary>One SMS for each cheque and each instalment, to the Owner (§10), never a summary.</summary>
    private static async Task<List<Notification>> PayablesComingDueAsync(
        IAppDbContext db, DateOnly today, int daysBefore, string template, string ownerPhone, CancellationToken cancellationToken)
    {
        var dueBy = today.AddDays(daysBefore);

        var payables = await db.Payables
            .AsNoTracking()
            .Where(payable => payable.PaidAt == null && payable.CancelledAt == null)
            .Where(payable => payable.DueDate >= today && payable.DueDate <= dueBy)
            .Where(payable => !db.Notifications.Any(notification => notification.PayableId == payable.Id))
            .OrderBy(payable => payable.DueDate)
            .ThenBy(payable => payable.Id)
            .ToListAsync(cancellationToken);

        return payables
            .Where(payable => SmsAudience.IsComingDue(payable, today, daysBefore))
            .Select(payable => Notification.ForPayableDue(
                payable.Id,
                ownerPhone,
                template,
                SmsValues.ForPayableDue(payable.Kind, payable.Amount, payable.DueDate, payable.Payee)))
            .ToList();
    }

    /// <summary>
    /// The plans that can be running out today, with their member's name and number: not a single
    /// visit, not cancelled or frozen, started and not ended, sessions left; the member active and not
    /// renewed (§10); no message of this kind yet.
    /// </summary>
    /// <remarks>
    /// "Renewed" is the desk's own test (§6 <i>The desk panel</i>): another subscription of the member,
    /// not cancelled, not frozen, not a single visit, that starts after today. Once it is cancelled the
    /// member counts as not renewed again.
    /// </remarks>
    private static IQueryable<PlanRow> ActivePlansOfActiveMembers(IAppDbContext db, DateOnly today, NotificationKind kind) =>
        db.Subscriptions
            .AsNoTracking()
            .Where(subscription => !subscription.IsSingleSession
                && subscription.CancelledAt == null
                && subscription.FrozenSince == null
                && subscription.StartDate <= today
                && subscription.EndDate >= today
                && subscription.UsedSessions < subscription.TotalSessions)
            .Where(subscription => !db.Notifications.Any(notification =>
                notification.SubscriptionId == subscription.Id && notification.Kind == kind))
            .Where(subscription => !db.Subscriptions.Any(next =>
                next.MemberId == subscription.MemberId
                && next.CancelledAt == null
                && next.FrozenSince == null
                && !next.IsSingleSession
                && next.StartDate > today))
            .Join(
                db.Members.Where(member => member.IsActive),
                subscription => subscription.MemberId,
                member => member.Id,
                (subscription, member) => new PlanRow
                {
                    Subscription = subscription,
                    FullName = member.FullName,
                    PhoneNumber = member.PhoneNumber,
                });

    /// <summary>Set with an initializer, not a constructor, so EF Core can still filter and sort on it.</summary>
    private sealed class PlanRow
    {
        public required Domain.Subscriptions.Subscription Subscription { get; init; }

        public required string FullName { get; init; }

        public required string PhoneNumber { get; init; }
    }
}
