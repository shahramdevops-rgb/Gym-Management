namespace Gym.Application.Payments;

/// <summary>
/// The «تسویه یکجا» a payment row was written by (BUSINESS_RULES.md §5 <i>Settling several items at
/// once</i>), so a payment history can show its rows under one heading. The total and the count are
/// the whole handover's, not only the rows a filter or a page happens to show.
/// </summary>
/// <param name="Id">Shared by every row of the settlement.</param>
/// <param name="Total">The one amount the desk took.</param>
/// <param name="ItemCount">How many items it paid.</param>
public sealed record SettlementSummary(Guid Id, decimal Total, int ItemCount);
