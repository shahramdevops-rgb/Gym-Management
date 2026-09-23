using Gym.Application.Common;
using Gym.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.ServiceCharges;

/// <summary>
/// The live charges of one or more visits, ready to hang off an attendance row
/// (BUSINESS_RULES.md §7 <i>Gym services</i>).
/// </summary>
/// <remarks>
/// Two queries for a whole page rather than a pair per row, the same shape as
/// <c>MemberDebt.GetTotalsAsync</c>. It is a separate step instead of part of
/// <c>AttendanceResponse.Projection</c> because a charge's payment status is calculated in C#
/// (<see cref="PaymentStatusCalculator"/>), which EF Core cannot translate into SQL.
/// </remarks>
public static class VisitServiceCharges
{
    /// <summary>
    /// The non-voided charges of <paramref name="attendanceIds"/>, keyed by visit. A visit with no
    /// charge is simply absent, which is the ordinary case.
    /// </summary>
    public static async Task<Dictionary<Guid, List<ServiceChargeResponse>>> ByAttendanceAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> attendanceIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(attendanceIds);

        if (attendanceIds.Count == 0)
        {
            return [];
        }

        var charges = await db.ServiceCharges.AsNoTracking()
            .Where(charge => attendanceIds.Contains(charge.AttendanceId) && charge.VoidedAt == null)
            .ToListAsync(cancellationToken);

        if (charges.Count == 0)
        {
            return [];
        }

        var chargeIds = charges.Select(charge => charge.Id).ToList();

        var netPaidById = await db.Payments.AsNoTracking()
            .Where(payment => payment.ServiceChargeId != null && chargeIds.Contains(payment.ServiceChargeId!.Value))
            .GroupBy(payment => payment.ServiceChargeId!.Value)
            .Select(group => new
            {
                ServiceChargeId = group.Key,
                NetPaid = group.Sum(payment => payment.Kind == PaymentKind.Payment ? payment.Amount : -payment.Amount),
            })
            .ToDictionaryAsync(row => row.ServiceChargeId, row => row.NetPaid, cancellationToken);

        var openVisits = await db.Attendances.AsNoTracking()
            .Where(attendance => attendanceIds.Contains(attendance.Id) && attendance.CheckedOutAt == null)
            .Select(attendance => attendance.Id)
            .ToListAsync(cancellationToken);

        var openVisitIds = openVisits.ToHashSet();

        return charges
            .GroupBy(charge => charge.AttendanceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(charge => ServiceChargeResponse.From(
                        charge, netPaidById.GetValueOrDefault(charge.Id), openVisitIds.Contains(charge.AttendanceId)))
                    .OrderBy(charge => charge.Kind)
                    .ToList());
    }
}
