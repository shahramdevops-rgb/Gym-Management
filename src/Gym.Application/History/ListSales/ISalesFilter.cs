namespace Gym.Application.History.ListSales;

/// <summary>
/// Which sales a request is about: the filters the sales list (<see cref="ListSalesQuery"/>) and
/// its totals (<c>SalesTotalsQuery</c>, roadmap 6.5.32) share. Both go through
/// <see cref="SaleRows"/>, so the totals always add up exactly the rows the list pages through.
/// </summary>
public interface ISalesFilter
{
    /// <summary>Inclusive, compared with the day each sale belongs to (BUSINESS_RULES.md §12).</summary>
    DateOnly? From { get; }

    /// <summary>Inclusive. <c>null</c> means no upper bound.</summary>
    DateOnly? To { get; }

    /// <summary>One member's sales; <c>null</c> means everyone's, walk-in and guest sales included.</summary>
    Guid? MemberId { get; }

    /// <summary>Only one kind of sale; <c>null</c> means all of them («همهٔ فروش‌ها»).</summary>
    SaleSource? Source { get; }

    /// <summary>Only fully paid, or only still owed; <c>null</c> means every sale, cancelled and voided included.</summary>
    SalePaidFilter? Paid { get; }
}
