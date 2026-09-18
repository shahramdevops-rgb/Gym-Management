using Gym.Application.Common;

namespace Gym.Api.IntegrationTests.Common;

/// <summary>A current user the test sets directly, for code tested without an HTTP request.</summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }

    public string? IpAddress { get; set; }
}
