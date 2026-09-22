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

    /// <summary>A birth date further back than this is a typing slip (BUSINESS_RULES.md §2).</summary>
    public const int MaxAgeYears = 120;

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

    /// <summary>
    /// Optional and usually empty: the gym has no birth date for anyone who joined before this
    /// field existed, and staff are never made to invent one (BUSINESS_RULES.md §2).
    /// </summary>
    public DateOnly? BirthDate { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Postgres <c>xmin</c>. Two receptionists editing the same member: the second save fails
    /// instead of silently overwriting the first person's change.
    /// </summary>
    public uint Version { get; private set; }

    /// <param name="today">
    /// The gym's today (Asia/Tehran). A birth date can only be judged against a date, and which
    /// date that is belongs to the application, not to a clock this entity reads for itself.
    /// </param>
    public static Result<Member> Create(string fullName, string phoneNumber, string? notes, DateOnly? birthDate, DateOnly today)
    {
        var member = new Member { IsActive = true };
        var result = member.Update(fullName, phoneNumber, notes, birthDate, today);

        return result.IsSuccess ? member : Result.Failure<Member>(result.Error);
    }

    /// <summary>
    /// Replaces the member's details. Allowed while inactive too: correcting a phone number
    /// before reactivating is a normal thing to do (BUSINESS_RULES.md §2).
    /// </summary>
    /// <param name="today">The gym's today, as in <see cref="Create"/>.</param>
    public Result Update(string fullName, string phoneNumber, string? notes, DateOnly? birthDate, DateOnly today)
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

        if (birthDate is { } born)
        {
            if (born > today)
            {
                return Result.Failure(MemberErrors.BirthDateInFuture);
            }

            // AddYears moves 29 February to the 28th in a non-leap year, which can only make the
            // boundary a day more generous. Exactly 120 years ago is still allowed; older is not.
            if (born < today.AddYears(-MaxAgeYears))
            {
                return Result.Failure(MemberErrors.BirthDateTooOld);
            }
        }

        FullName = name;
        NormalizedFullName = PersianText.Normalize(name).ToLowerInvariant();
        PhoneNumber = phoneNumber;
        Notes = cleanNotes;
        BirthDate = birthDate;

        return Result.Success();
    }

    /// <summary>
    /// An inactive member cannot check in or receive new subscriptions (BUSINESS_RULES.md §2).
    /// Deactivating an inactive member succeeds and changes nothing.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reactivating an active member succeeds and changes nothing.</summary>
    public void Reactivate() => IsActive = true;

    /// <summary>
    /// BUSINESS_RULES.md §2: inactive members cannot receive new subscriptions. Selling asks this
    /// instead of reading <see cref="IsActive"/>, like <c>Plan.EnsureCanBeSold</c>.
    /// </summary>
    public Result EnsureCanReceiveSubscription() =>
        IsActive ? Result.Success() : Result.Failure(MemberErrors.Inactive);

    /// <summary>BUSINESS_RULES.md §7: inactive members cannot check in.</summary>
    public Result EnsureCanCheckIn() =>
        IsActive ? Result.Success() : Result.Failure(MemberErrors.Inactive);

    private static bool IsE164(string value) =>
        value.Length is >= 8 and <= 16 && value[0] == '+' && value.Skip(1).All(char.IsAsciiDigit);
}
