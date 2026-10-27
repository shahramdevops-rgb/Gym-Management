using Gym.Domain.Common;

namespace Gym.Domain.SiteAuth;

/// <summary>
/// An account on the site (BUSINESS_RULES.md §15, ADR 0010): a member's, a trainer's, or both when
/// one person is both with one phone. Not a staff user and not an Identity user, with no password.
/// Made the first time the phone signs in with a code.
/// </summary>
/// <remarks>
/// <para>
/// <b>No phone of its own.</b> A sign-in finds the member and the trainer by the verified phone,
/// and the account by them, so the panel's phones are the only ones there are. Had the account kept
/// its own copy, a number the desk changed would still open the account for whoever has the old
/// number now. The unique indexes on <see cref="MemberId"/> and <see cref="TrainerId"/> keep each
/// to one account.
/// </para>
/// <para>
/// <b>Both links follow the phone.</b> Every sign-in sets <see cref="TrainerId"/> to the trainer
/// the phone belongs to now; the Owner changing a trainer's phone clears it. An account with
/// neither link is never found again, and its sessions have been ended.
/// </para>
/// </remarks>
public sealed class SiteAccount : Entity
{
    // For EF Core.
    private SiteAccount()
    {
    }

    /// <summary>The member this account is, if any. Unique.</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>The trainer this account is, if any, stopped or not. Unique.</summary>
    public Guid? TrainerId { get; private set; }

    public DateTimeOffset LastSignedInAt { get; private set; }

    public static SiteAccount ForMember(Guid memberId, DateTimeOffset now) =>
        new() { MemberId = memberId, LastSignedInAt = now };

    public static SiteAccount ForTrainer(Guid trainerId, DateTimeOffset now) =>
        new() { TrainerId = trainerId, LastSignedInAt = now };

    /// <summary>Each sign-in with a code; a refresh is not a sign-in.</summary>
    public void RecordSignIn(DateTimeOffset now) => LastSignedInAt = now;

    /// <summary>The phone signed in and belongs to this member too.</summary>
    public void LinkMember(Guid memberId) => MemberId = memberId;

    /// <summary>The phone signed in and belongs to this trainer now.</summary>
    public void LinkTrainer(Guid trainerId) => TrainerId = trainerId;

    /// <summary>The trainer's phone changed, or now belongs to another account.</summary>
    public void UnlinkTrainer() => TrainerId = null;
}
