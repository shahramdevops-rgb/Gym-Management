namespace Gym.Application.Auth.Logout;

/// <summary>The refresh token from the cookie. Null when the browser sent no cookie.</summary>
public sealed record LogoutCommand(string? RefreshToken);
