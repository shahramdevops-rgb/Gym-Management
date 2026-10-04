using Gym.Application.Common;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges;

/// <summary>
/// The row lock that serializes a charge's payments, amount changes and void. A member's charge
/// takes the member's lock, the one every payment of theirs takes. A guest's charge has no member
/// (BUSINESS_RULES.md §7 <i>Guest visit</i>), so it takes its visit's lock, the one the guest's
/// check-out, cancel and «تسویه یکجا» take, as a guest's cafe order does.
/// </summary>
public static class ServiceChargeLock
{
    /// <summary>Takes the lock inside the caller's transaction.</summary>
    public static Task TakeAsync(IAppDbContext db, ServiceCharge charge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(charge);

        return charge.MemberId is { } memberId
            ? db.LockMemberAsync(memberId, cancellationToken)
            : db.LockAttendanceAsync(charge.AttendanceId, cancellationToken);
    }
}
