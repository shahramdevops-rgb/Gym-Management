using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.History.ListPayments;

/// <summary>
/// Which payments and refunds a request is about: the filters the payments list
/// (<see cref="ListPaymentsQuery"/>) and its totals (<c>PaymentTotalsQuery</c>, roadmap 6.5.32)
/// share. Both go through <see cref="PaymentRows"/>, so the totals add up exactly the rows listed.
/// What each filter means is on <see cref="ListPaymentsQuery"/>.
/// </summary>
public interface IPaymentsFilter
{
    DateOnly? From { get; }

    DateOnly? To { get; }

    Guid? MemberId { get; }

    PaymentMethod? Method { get; }

    PaymentTargetKind? Source { get; }

    ServiceChargeKind? ServiceKind { get; }
}
