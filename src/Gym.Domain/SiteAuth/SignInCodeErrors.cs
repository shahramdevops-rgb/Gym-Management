using Gym.Domain.Common;

namespace Gym.Domain.SiteAuth;

/// <summary>The one-time code's failures (BUSINESS_RULES.md §15). The site maps each code to Persian.</summary>
public static class SignInCodeErrors
{
    /// <summary>Expired, used, or ended by 5 wrong tries: a new code is needed.</summary>
    public static readonly Error Expired = Error.BusinessRule(
        "SiteAuth.CodeExpired",
        "The code is no longer valid; ask for a new one.");

    public static readonly Error Wrong = Error.BusinessRule(
        "SiteAuth.CodeWrong",
        "The code is not right.");

    /// <summary>The fifth wrong try: the code is dead from now on.</summary>
    public static readonly Error TooManyWrongTries = Error.BusinessRule(
        "SiteAuth.CodeTooManyWrongTries",
        "Too many wrong tries; ask for a new code.");

    public static readonly Error PhoneHourlyLimitReached = Error.BusinessRule(
        "SiteAuth.PhoneHourlyLimitReached",
        "This phone has been sent the most codes allowed in an hour.");

    public static readonly Error GymDailyLimitReached = Error.BusinessRule(
        "SiteAuth.GymDailyLimitReached",
        "The gym has sent the most codes allowed today.");

    /// <summary>Kavenegar refused the SMS or could not be reached; nothing was sent and nothing paid.</summary>
    public static readonly Error NotSent = Error.BusinessRule(
        "SiteAuth.CodeNotSent",
        "The code could not be sent; try again later.");

    // Never reach the API: the handler answers these cases with the code the phone already has.
    public static readonly Error StillValid = Error.Conflict(
        "SiteAuth.CodeStillValid",
        "The current code still works.");

    public static readonly Error SendAgainTooSoon = Error.Conflict(
        "SiteAuth.SendAgainTooSoon",
        "The code was sent less than two minutes ago.");
}
