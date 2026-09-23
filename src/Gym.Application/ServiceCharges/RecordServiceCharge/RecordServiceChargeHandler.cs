using Gym.Application.Common;
using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.ServiceCharges;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.ServiceCharges.RecordServiceCharge;

/// <summary>
/// Puts an amount on something the member used during an open visit — today only هوازی
/// (BUSINESS_RULES.md §7 <i>Gym services</i>). Front desk work, so both roles.
/// </summary>
public sealed class RecordServiceChargeHandler(
    IAppDbContext db, IGymCalendar calendar, ICurrentUser currentUser)
{
    public async Task<Result<ServiceChargeResponse>> Handle(
        Guid attendanceId, RecordServiceChargeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attendance = await db.Attendances.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == attendanceId, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<ServiceChargeResponse>(AttendanceErrors.NotFound);
        }

        // BUSINESS_RULES.md §7: only an open visit. CheckedOutAt covers all three ways a visit
        // closes — the member checked out, the nightly job closed it, or the check-in was
        // cancelled — which is the same column the partial unique indexes filter on.
        if (attendance.CheckedOutAt is not null)
        {
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.VisitNotOpen);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Recording a service charge was called without an authenticated user.");

        var recorded = ServiceCharge.Record(
            attendance.MemberId, attendanceId, command.Kind, command.Amount, calendar.Today(), userId);
        if (recorded.IsFailure)
        {
            return Result.Failure<ServiceChargeResponse>(recorded.Error);
        }

        db.ServiceCharges.Add(recorded.Value);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ServiceChargeConstraints.OneLivePerVisitAndKind)
        {
            // BUSINESS_RULES.md §7: one live charge per visit per kind. Two desks recording at the
            // same moment is the only way past a check on a row that already exists, so the index
            // is what actually enforces it and this only translates its complaint.
            return Result.Failure<ServiceChargeResponse>(ServiceChargeErrors.AlreadyCharged);
        }

        return ServiceChargeResponse.From(recorded.Value, netPaid: 0, visitIsOpen: true);
    }
}
