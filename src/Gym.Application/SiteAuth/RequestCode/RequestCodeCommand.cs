namespace Gym.Application.SiteAuth.RequestCode;

/// <param name="PhoneNumber">As typed: <c>0912…</c>, <c>۰۹۱۲…</c> or <c>+98 912…</c>.</param>
public sealed record RequestCodeCommand(string PhoneNumber);
