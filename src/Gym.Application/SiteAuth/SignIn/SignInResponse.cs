using System.Text.Json.Serialization;

namespace Gym.Application.SiteAuth.SignIn;

/// <summary>
/// After the code: either signed in, or a new phone that must first give its name and birth date
/// (BUSINESS_RULES.md §15).
/// </summary>
/// <param name="Access">Set when <see cref="Outcome"/> is <see cref="SignInOutcome.SignedIn"/>.</param>
/// <param name="SignUpTicket">
/// Set when <see cref="Outcome"/> is <see cref="SignInOutcome.NeedsProfile"/>: sent back with the
/// name and birth date, within <see cref="SignUpTicketExpiresAt"/>.
/// </param>
public sealed record SignInResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<SignInOutcome>))] SignInOutcome Outcome,
    SiteAccessResponse? Access,
    string? SignUpTicket,
    DateTimeOffset? SignUpTicketExpiresAt);

public enum SignInOutcome
{
    /// <summary>The phone is a member's: signed in, linked to that member the first time.</summary>
    SignedIn,

    /// <summary>A phone the gym does not know: the sign-up screen comes next.</summary>
    NeedsProfile,
}

/// <summary>The body, and the session whose refresh token goes in the cookie when signed in.</summary>
public sealed record SignInResult(SignInResponse Response, SiteSession? Session);
