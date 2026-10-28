namespace Gym.Application.Notifications.GetSignInCodeSms;

/// <summary>
/// The site's sign-in code SMS, as «پیامک‌ها» shows them (BUSINESS_RULES.md §15): only counts and
/// cost, never a code or a phone.
/// </summary>
/// <param name="Today">Code SMS sent today (the gym's day), the sends again included.</param>
/// <param name="DailyLimit">The gym's 300 a day; reaching it stops the codes until tomorrow.</param>
/// <param name="ThisMonth">Code SMS sent since the 1st of this Jalali month.</param>
/// <param name="ThisMonthCostToman">
/// What Kavenegar charged for them, in Toman like every amount here (the provider's Rial ÷ 10); a
/// send that got no answer has no cost and is counted in <paramref name="ThisMonth"/> only.
/// </param>
public sealed record SignInCodeSmsResponse(int Today, int DailyLimit, int ThisMonth, decimal ThisMonthCostToman)
{
    public bool LimitReached => Today >= DailyLimit;
}
