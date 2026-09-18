namespace Gym.Application.Common.Security;

/// <summary>A signed access token and the moment it stops being accepted (UTC).</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
