using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications.GetSignInCodeSms;

/// <summary>
/// Counts the code SMS of the site's sign-in, today and this Jalali month (BUSINESS_RULES.md §15).
/// Read from <see cref="SignInCodeSend"/>, the rows the limits count, so the panel and the 300 a
/// day can never disagree.
/// </summary>
public sealed class GetSignInCodeSmsHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider timeProvider)
{
    public async Task<SignInCodeSmsResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.DayOf(timeProvider.GetUtcNow());
        var startOfToday = calendar.StartOfDayUtc(today);

        var (year, month, _) = JalaliCalendar.Parts(today);
        var startOfMonth = calendar.StartOfDayUtc(JalaliCalendar.ToDate(year, month, 1));

        var thisMonth = await db.SignInCodeSends
            .Where(send => send.SentAt >= startOfMonth)
            .GroupBy(_ => 1)
            .Select(sends => new
            {
                Count = sends.Count(),
                Today = sends.Count(send => send.SentAt >= startOfToday),
                CostRial = sends.Sum(send => send.CostRial ?? 0m),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new SignInCodeSmsResponse(
            thisMonth?.Today ?? 0,
            SignInCode.MaxSendsPerGymDay,
            thisMonth?.Count ?? 0,
            thisMonth?.CostRial ?? 0m);
    }
}
