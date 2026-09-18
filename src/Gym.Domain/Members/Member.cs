using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Domain.Members;

/// <summary>
/// A gym member (BUSINESS_RULES.md §2). Members are deactivated, never deleted.
/// </summary>
/// <remarks>
/// <para>
/// The name is kept twice: <see cref="FullName"/> as the user typed it, for display, and
/// <see cref="NormalizedFullName"/> for search. Only this class writes either, and it always
/// writes both, so the search column can never fall out of step with the name.
/// </para>
/// <para>
/// The phone number arrives already in E.164 (<c>+989121234567</c>): turning what someone
/// typed into that form needs libphonenumber, which lives behind <c>IPhoneNormalizer</c> in the
/// Application layer. The entity only insists that it received the canonical form, because the
/// unique index on this column is only as good as the normalization in front of it.
/// </para>
/// </remarks>
public sealed class Member : Entity
{
    public const int FullNameMaxLength = 200;
    public const int NotesMaxLength = 1000;

    // For EF Core.
    private Member()
    {
    }

    public string FullName { get; private set; } = string.Empty;

    /// <summary><see cref="FullName"/> through <see cref="PersianText.Normalize"/>, in lower case.</summary>
    public string NormalizedFullName { get; private set; } = string.Empty;

    /// <summary>E.164, for example <c>+989121234567</c>. Unique across all members.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Postgres <c>xmin</c>. Two receptionists editing the same member: the second save fails
    /// instead of silently overwriting the first person's change.
    /// </summary>
    public uint Version { get; private set; }

    public static Result<Member> Create(string fullName, string phoneNumber, string? notes)
    {
        var member = new Member { IsActive = true };
        var result = member.Update(fullName, phoneNumber, notes);

        return result.IsSuccess ? member : Result.Failure<Member>(result.Error);
    }

    /// <summary>
    /// Replaces the member's details. Allowed while inactive too: correcting a phone number
    /// before reactivating is a normal thing to do (BUSINESS_RULES.md §2).
    /// </summary>
    public Result Update(string fullName, string phoneNumber, string? notes)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        if (!IsE164(phoneNumber))
        {
            // A caller that skipped IPhoneNormalizer is a bug, not bad input from a user.
            throw new ArgumentException("The phone number must already be normalized to E.164.", nameof(phoneNumber));
        }

        var name = fullName.Trim();
        if (name.Length == 0)
        {
            return Result.Failure(MemberErrors.FullNameRequired);
        }

        if (name.Length > FullNameMaxLength)
        {
            return Result.Failure(MemberErrors.FullNameTooLong);
        }

        var cleanNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (cleanNotes?.Length > NotesMaxLength)
        {
            return Result.Failure(MemberErrors.NotesTooLong);
        }

        FullName = name;
        NormalizedFullName = PersianText.Normalize(name).ToLowerInvariant();
        PhoneNumber = phoneNumber;
        Notes = cleanNotes;

        return Result.Success();
    }

    /// <summary>
    /// An inactive member cannot check in or receive new subscriptions (BUSINESS_RULES.md §2).
    /// Deactivating an inactive member succeeds and changes nothing.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reactivating an active member succeeds and changes nothing.</summary>
    public void Reactivate() => IsActive = true;

    private static bool IsE164(string value) =>
        value.Length is >= 8 and <= 16 && value[0] == '+' && value.Skip(1).All(char.IsAsciiDigit);
}
