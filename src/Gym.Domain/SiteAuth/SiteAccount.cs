using Gym.Domain.Common;

namespace Gym.Domain.SiteAuth;

/// <summary>
/// A member's account on the site (BUSINESS_RULES.md §15, ADR 0010): not a staff user and not an
/// Identity user, with no password. Made the first time the member's phone signs in with a code;
/// a trainer's account comes with task 15.2.
/// </summary>
/// <remarks>
/// <b>No phone of its own.</b> A sign-in finds the member by the verified phone and the account by
/// the member, so the member's phone in the panel is the only one there is. Had the account kept
/// its own copy, a number the desk changed would still open the account for whoever has the old
/// number now. The unique index on <see cref="MemberId"/> keeps a member to one account.
/// </remarks>
public sealed class SiteAccount : Entity
{
    // For EF Core.
    private SiteAccount()
    {
    }

    /// <summary>The member this account is. Unique: a member has one account at most.</summary>
    public Guid MemberId { get; private set; }

    public DateTimeOffset LastSignedInAt { get; private set; }

    public static SiteAccount ForMember(Guid memberId, DateTimeOffset now) =>
        new() { MemberId = memberId, LastSignedInAt = now };

    /// <summary>Each sign-in with a code; a refresh is not a sign-in.</summary>
    public void RecordSignIn(DateTimeOffset now) => LastSignedInAt = now;
}
