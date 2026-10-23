using Gym.Application.Common;
using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.Broadcasts.GetBroadcastAudience;

/// <summary>
/// <c>GET /api/sms/broadcasts/audiences</c>: the size of each group and its longest first name, read
/// when the Owner opens «پیامک گروهی» (task 10.13). Owner only.
/// </summary>
public sealed class GetBroadcastAudienceHandler(BroadcastRecipients recipients, IGymCalendar calendar)
{
    public async Task<BroadcastAudiencesResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.Today();

        return new BroadcastAudiencesResponse(
            await GroupAsync(BroadcastAudience.ActiveMembers, today, cancellationToken),
            await GroupAsync(BroadcastAudience.MembersWithUsablePlan, today, cancellationToken));
    }

    private async Task<BroadcastGroupResponse> GroupAsync(
        BroadcastAudience audience, DateOnly today, CancellationToken cancellationToken)
    {
        var group = await recipients.FindAsync(audience, today, withoutMessageOf: null, cancellationToken);

        return new BroadcastGroupResponse(group.Count, BroadcastRecipients.LongestFirstName(group));
    }
}
