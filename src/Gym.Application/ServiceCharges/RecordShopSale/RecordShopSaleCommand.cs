namespace Gym.Application.ServiceCharges.RecordShopSale;

/// <summary>
/// What the desk sold from «فروشگاه» in one go (BUSINESS_RULES.md §7 <i>Sale at the desk</i>): one
/// or more items, each becoming its own charge on the member's account. No money is taken here;
/// it is paid afterwards like any other debt.
/// </summary>
public sealed record RecordShopSaleCommand(IReadOnlyList<ShopSaleItem> Items);

/// <param name="Description">What was sold, as the desk calls it.</param>
/// <param name="Quantity">How many, 1 to 999.</param>
/// <param name="UnitPrice">The price of one; the system never checks it against anything.</param>
public sealed record ShopSaleItem(string Description, int Quantity, decimal UnitPrice);
