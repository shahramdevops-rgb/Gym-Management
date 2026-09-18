namespace Gym.Application.Auth.Login;

/// <summary>
/// The access token and when it expires, so the frontend knows when to refresh (task 1.3).
/// <see cref="MustChangePassword"/> is repeated outside the token so the frontend can open the
/// change-password screen without decoding the JWT.
/// </summary>
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, bool MustChangePassword);
