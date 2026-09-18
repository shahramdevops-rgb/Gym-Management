namespace Gym.Application.Auth.Refresh;

/// <summary>The refresh token from the cookie. Null when the browser sent no cookie.</summary>
public sealed record RefreshCommand(string? RefreshToken);
