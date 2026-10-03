namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Bodies for <c>POST /api/attendance/{id}/cancel</c>, which since roadmap 6.5.8 must say what
/// happens to the visit's purchases (BUSINESS_RULES.md §7 <i>Cancel check-in</i>).
/// </summary>
internal static class CancelCheckInBody
{
    /// <summary>Nothing the visit bought is cancelled: the choice of every test not about purchases.</summary>
    internal static object KeepPurchases { get; } = new
    {
        voidCardio = false,
        cafeOrderIds = Array.Empty<Guid>(),
        miscellaneousSaleIds = Array.Empty<Guid>(),
    };

    internal static object Cancel(bool voidCardio, params Guid[] cafeOrderIds) =>
        new { voidCardio, cafeOrderIds, miscellaneousSaleIds = Array.Empty<Guid>() };

    /// <summary>Voids the named miscellaneous sales (task 6.5.28) and keeps everything else.</summary>
    internal static object VoidMiscellaneous(params Guid[] miscellaneousSaleIds) =>
        new { voidCardio = false, cafeOrderIds = Array.Empty<Guid>(), miscellaneousSaleIds };
}
