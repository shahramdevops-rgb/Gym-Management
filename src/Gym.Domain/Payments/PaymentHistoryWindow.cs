using Gym.Domain.Common;

namespace Gym.Domain.Payments;

/// <summary>
/// How far back each role may read the gym's payments (BUSINESS_RULES.md §12 <i>History</i>, §1):
/// Staff see today and the <see cref="StaffDaysBeforeToday"/> days before it, the Owner everything.
/// </summary>
/// <remarks>
/// A rule about the request, not about a payment, so it takes the role as a plain flag and the
/// gym's today as a parameter: Domain knows neither who is asking nor what day it is.
/// </remarks>
public static class PaymentHistoryWindow
{
    /// <summary>Today 1405/07/10 → Staff may start at 07/07.</summary>
    public const int StaffDaysBeforeToday = 3;

    /// <summary>The first day Staff may ask for.</summary>
    public static DateOnly EarliestForStaff(DateOnly today) => today.AddDays(-StaffDaysBeforeToday);

    /// <summary>
    /// Whether a payments request starting at <paramref name="from"/> is within reach. A request
    /// with no start is unbounded, which reaches as far back as there are payments, so for Staff
    /// it is refused like any other range that starts too early.
    /// </summary>
    public static Result Check(DateOnly? from, bool isOwner, DateOnly today)
    {
        if (isOwner)
        {
            return Result.Success();
        }

        return from is { } start && start >= EarliestForStaff(today)
            ? Result.Success()
            : Result.Failure(PaymentErrors.HistoryTooFarBack);
    }
}
