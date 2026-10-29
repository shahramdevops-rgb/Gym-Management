using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Domain.Common;
using Gym.Domain.Common.Text;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteAuth.VerifyCode;

/// <summary>
/// Checks the code a phone typed (BUSINESS_RULES.md §15) and answers with the phone it proves, in
/// E.164. A wrong code costs a try; the fifth ends the code; the right one is used up.
/// </summary>
/// <remarks>
/// <para>
/// It signs nobody in by itself: the site's sign-in (task 15.1b) finds or makes the account in the
/// same transaction, through <see cref="VerifyLockedAsync"/>, so the code is used up only when the
/// sign-in itself is saved (15.3b). <see cref="Handle"/> checks a code alone, in a transaction of its
/// own, and saves its change either way, because a wrong try must be counted even when the request
/// answers with a failure.
/// </para>
/// <para>
/// The phone is locked as in <c>RequestCodeHandler</c>, so two guesses at the same moment are both
/// counted instead of one overwriting the other's try.
/// </para>
/// </remarks>
public sealed class VerifyCodeHandler(
    IAppDbContext db,
    IPhoneNormalizer phones,
    ISignInCodes codes,
    IGymCalendar calendar,
    TimeProvider timeProvider)
{
    public async Task<Result<string>> Handle(VerifyCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var phone = phones.Normalize(command.PhoneNumber);
        if (phone.IsFailure)
        {
            return Result.Failure<string>(phone.Error);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockPhoneAsync(phone.Value, cancellationToken);

        var verified = await VerifyLockedAsync(phone.Value, command.Code, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return verified;
    }

    /// <summary>
    /// The check itself, for a caller that already holds a transaction and the phone's lock
    /// (<c>LockPhoneAsync</c>). The try or the use is saved but not committed: the caller commits it
    /// with its own work, and a failure of that work takes the use back with it.
    /// </summary>
    public async Task<Result<string>> VerifyLockedAsync(string phoneNumber, string typedCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentNullException.ThrowIfNull(typedCode);

        var typed = PersianText.NormalizeDigits(typedCode).Trim();

        var code = await db.SignInCodes.SingleOrDefaultAsync(row => row.PhoneNumber == phoneNumber, cancellationToken);
        if (code is null)
        {
            return Result.Failure<string>(SignInCodeErrors.Expired);
        }

        var now = timeProvider.GetUtcNow();
        var verified = code.Verify(typed, codes.CodeFor(phoneNumber, code.CodeSeed), now, calendar.DayOf(now));

        await db.SaveChangesAsync(cancellationToken);

        return verified.IsSuccess ? phoneNumber : Result.Failure<string>(verified.Error);
    }
}
