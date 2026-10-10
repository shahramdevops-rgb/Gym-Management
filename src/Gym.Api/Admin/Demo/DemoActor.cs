using Gym.Application.Common;

namespace Gym.Api.Admin.Demo;

/// <summary>
/// Who <c>admin seed-demo</c> is acting as at the moment: the Owner for the office work, the two
/// members of staff for the desk. There is no HTTP request to read a user from, and the audit
/// fields and the history's «ثبت توسط» should show real accounts rather than nobody.
/// </summary>
public sealed class DemoActor : ICurrentUser
{
    public Guid? UserId { get; private set; }

    public bool IsOwner { get; private set; }

    public string? IpAddress => null;

    public void ActAs(Guid userId, bool isOwner)
    {
        UserId = userId;
        IsOwner = isOwner;
    }

    /// <summary>The background jobs (the night's check-out, the SMS runs) work on nobody's behalf.</summary>
    public void ActAsNobody()
    {
        UserId = null;
        IsOwner = false;
    }
}
