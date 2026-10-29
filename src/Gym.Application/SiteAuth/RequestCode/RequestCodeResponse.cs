using System.Text.Json.Serialization;

namespace Gym.Application.SiteAuth.RequestCode;

/// <summary>What the sign-in screen shows after «دریافت کد» or «ارسال دوباره» (BUSINESS_RULES.md §15).</summary>
/// <param name="ExpiresAt">When the code the phone has stops working.</param>
/// <param name="SendAgainAt">
/// When «ارسال دوباره» may be pressed: 2 minutes after the last send, or, once Kavenegar has
/// delivered the code, when it expires (a new code is then made).
/// </param>
public sealed record RequestCodeResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<RequestCodeOutcome>))] RequestCodeOutcome Outcome,
    DateTimeOffset ExpiresAt,
    DateTimeOffset SendAgainAt);

public enum RequestCodeOutcome
{
    /// <summary>A new code went out.</summary>
    Sent,

    /// <summary>The same code went out again («ارسال دوباره»).</summary>
    SentAgain,

    /// <summary>Nothing sent: the code went out less than 2 minutes ago («کد قبلاً برای شما پیامک شده است»).</summary>
    AlreadySent,

    /// <summary>Nothing sent: Kavenegar reported the code delivered, so it is on the phone already.</summary>
    AlreadyDelivered,
}
