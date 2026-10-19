using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.Broadcasts;

/// <summary>One member a broadcast goes to, as it stands now.</summary>
/// <param name="FirstName">What the message calls them (<see cref="SmsText.FirstName"/>).</param>
public sealed record BroadcastRecipient(Guid MemberId, string FullName, string PhoneNumber, string FirstName);

/// <summary>
/// The members of a broadcast's group (BUSINESS_RULES.md §10 <i>The broadcast</i>, task 10.13), read
/// as they stand now: by the page to count them, by the send to check the length, and by the job to
/// write their messages.
/// </summary>
/// <remarks>
/// <b>The query narrows, <see cref="SmsAudience"/> decides</b>, as for the daily kinds: the plans read
/// here are the ones that could be usable today, and the rule says which are.
/// </remarks>
public sealed class BroadcastRecipients(IAppDbContext db)
{
    /// <param name="withoutMessageOf">
    /// A broadcast whose recipients are left out: continuing it reaches only those it has not written to.
    /// </param>
    public async Task<List<BroadcastRecipient>> FindAsync(
        BroadcastAudience audience, DateOnly today, Guid? withoutMessageOf, CancellationToken cancellationToken)
    {
        var members = db.Members.AsNoTracking().Where(member => member.IsActive);
        if (withoutMessageOf is { } broadcastId)
        {
            members = members.Where(member => !db.Notifications.Any(
                notification => notification.BroadcastId == broadcastId && notification.MemberId == member.Id));
        }

        var rows = await members
            .OrderBy(member => member.Id)
            .Select(member => new { member.Id, member.FullName, member.PhoneNumber })
            .ToListAsync(cancellationToken);

        var plans = audience == BroadcastAudience.MembersWithUsablePlan
            ? (await db.Subscriptions.AsNoTracking()
                    .Where(plan => !plan.IsSingleSession && plan.CancelledAt == null && plan.FrozenSince == null &&
                        plan.StartDate <= today && plan.EndDate >= today && plan.UsedSessions < plan.TotalSessions)
                    .ToListAsync(cancellationToken))
                .ToLookup(plan => plan.MemberId)
            : null;

        return rows
            .Where(row => SmsAudience.IsInBroadcastGroup(audience, memberIsActive: true, plans?[row.Id] ?? [], today))
            .Select(row => new BroadcastRecipient(row.Id, row.FullName, row.PhoneNumber, SmsText.FirstName(row.FullName)))
            .ToList();
    }

    /// <summary>The longest first name among them, which the 3-part limit is measured with.</summary>
    public static string LongestFirstName(IEnumerable<BroadcastRecipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);

        return recipients.Select(recipient => recipient.FirstName).MaxBy(name => name.Length) ?? string.Empty;
    }
}
