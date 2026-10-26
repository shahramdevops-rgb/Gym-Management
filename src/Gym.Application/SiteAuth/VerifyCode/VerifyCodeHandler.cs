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
/// It signs nobody in by itself: the site's sign-in (task 15.1b) calls it and then finds or makes
/// the account. It saves its own change either way, because a wrong try must be counted even when
/// the request answers with a failure.
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

        var phoneNumber = phone.Value;
        var typed = PersianText.NormalizeDigits(command.Code).Trim();

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockPhoneAsync(phoneNumber, cancellationToken);

        var code = await db.SignInCodes.SingleOrDefaultAsync(row => row.PhoneNumber == phoneNumber, cancellationToken);
        if (code is null)
        {
            return Result.Failure<string>(SignInCodeErrors.Expired);
        }

        var now = timeProvider.GetUtcNow();
        var verified = code.Verify(typed, codes.CodeFor(phoneNumber, code.CodeSeed), now, calendar.DayOf(now));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return verified.IsSuccess ? phoneNumber : Result.Failure<string>(verified.Error);
    }
}
