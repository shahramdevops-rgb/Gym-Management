using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables.ListPayablesDueSoon;

/// <summary>
/// The alert in the header, on every page (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>).
/// Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// The dashboard's list starts 7 days ahead; this one starts later, at 5, because it is in sight
/// on every page and should only speak up when a payment is close. A pending one past its date
/// stays, like on the dashboard, until the Owner marks it. Its own small query rather than the
/// dashboard's, which also reads every member list.
/// </remarks>
public sealed class ListPayablesDueSoonHandler(IAppDbContext db, IGymCalendar calendar)
{
    /// <summary>A pending cheque or instalment is in the header this many days before its date, today included.</summary>
    public const int WithinDays = 5;

    public async Task<PayablesDueSoonResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.Today();
        var dueBy = today.AddDays(WithinDays);

        var payables = await db.Payables
            .AsNoTracking()
            .Where(payable => payable.PaidAt == null && payable.CancelledAt == null && payable.DueDate <= dueBy)
            .OrderBy(payable => payable.DueDate)
            .ThenBy(payable => payable.Id)
            .ToListAsync(cancellationToken);

        var items = payables
            .Select(payable => new PayableDueSoonResponse(
                payable.Id,
                payable.Kind,
                payable.Payee,
                payable.Amount,
                payable.DueDate,
                payable.InstallmentNumber,
                payable.InstallmentCount,
                payable.DueDate.DayNumber - today.DayNumber))
            .ToList();

        return new PayablesDueSoonResponse(today, items);
    }
}
