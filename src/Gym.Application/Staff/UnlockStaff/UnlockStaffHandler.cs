using Gym.Domain.Common;

namespace Gym.Application.Staff.UnlockStaff;

/// <summary>
/// The Owner unlocks a staff account without changing its password (BUSINESS_RULES.md §1
/// *Lockout*): the usual reason for a lockout is someone else's wrong guesses, and the staff
/// member still knows their own password.
/// </summary>
/// <remarks>
/// Sessions are left alone, unlike a reset: nothing suggests the password is known to anyone else.
/// </remarks>
public sealed class UnlockStaffHandler(IStaffAccounts staff)
{
    public Task<Result<StaffResponse>> Handle(Guid id, CancellationToken cancellationToken) =>
        staff.UnlockAsync(id, cancellationToken);
}
