namespace Gym.Application.Pricing.UpdatePrices;

/// <param name="Version">The <c>version</c> from the prices as they were read before editing.</param>
public sealed record UpdatePricesCommand(decimal SessionPrice, decimal SingleVisitPrice, uint Version);
