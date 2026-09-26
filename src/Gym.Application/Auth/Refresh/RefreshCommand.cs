namespace Gym.Application.Auth.Refresh;

/// <summary>The refresh token and the trusted-device secret from their cookies. Null for a missing cookie.</summary>
public sealed record RefreshCommand(string? RefreshToken, string? DeviceToken = null);
