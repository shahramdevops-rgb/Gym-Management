using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Application.Common.Sms;
using Gym.Domain.Common;
using Gym.Domain.Notifications;
using Gym.Domain.SiteAuth;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Gym.Application.SiteAuth.RequestCode;

/// <summary>
/// «دریافت کد» and «ارسال دوباره» on the site's sign-in screen (BUSINESS_RULES.md §15 <i>The
/// one-time code</i>). Every code is a paid SMS, so every limit is checked before Kavenegar is called.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the phone gets:</b>
/// <list type="bullet">
/// <item>No usable code (none yet, expired, used, or 5 wrong tries): a new code, valid 60 minutes.</item>
/// <item>A usable code sent less than 2 minutes ago: nothing; the screen says it was already sent.</item>
/// <item>A usable code Kavenegar reported delivered: nothing; it is on the phone.</item>
/// <item>Otherwise: the same code again, with the minutes it has left.</item>
/// </list>
/// </para>
/// <para>
/// <b>One request per phone at a time.</b> The phone is locked for the transaction, Kavenegar's call
/// included. Without it, two presses at once would both find no code and send two SMS.
/// </para>
/// <para>
/// <b>One send at a time for the whole gym.</b> Just before the limits are counted, the gym's lock is
/// taken too, and held until the send is saved; otherwise every phone asking while Kavenegar answers
/// would read the same count, and the day's 300 could be passed by as many. Sends are rare (300 a
/// day at most), so they waiting for one another costs nothing; an answer that sends nothing never
/// takes this lock. Always after the phone's lock, never before, so two requests cannot deadlock.
/// </para>
/// <para>
/// <b>Once Kavenegar is called, nothing cancels the request.</b> A browser that goes away must not
/// roll back an SMS that may already be paid for: the send would then count toward no limit, and a
/// new code's seed would be lost. From the send on, the work runs to the end on its own; Kavenegar's
/// own 30-second timeout still bounds it. The transaction holds a database connection meanwhile,
/// up to Kavenegar's two calls (the delivery question and the send), which a gym's traffic allows.
/// </para>
/// </remarks>
public sealed partial class RequestCodeHandler(
    IAppDbContext db,
    IPhoneNormalizer phones,
    ISignInCodes codes,
    IVerificationCodeSender sender,
    ISmsAccount account,
    IGymCalendar calendar,
    TimeProvider timeProvider,
    ILogger<RequestCodeHandler> logger)
{
    public async Task<Result<RequestCodeResponse>> Handle(RequestCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var phone = phones.Normalize(command.PhoneNumber);
        if (phone.IsFailure)
        {
            return Result.Failure<RequestCodeResponse>(phone.Error);
        }

        var phoneNumber = phone.Value;

        // Rolled back on every path that does not commit: an answer that sends nothing writes nothing.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockPhoneAsync(phoneNumber, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var current = await db.SignInCodes.SingleOrDefaultAsync(code => code.PhoneNumber == phoneNumber, cancellationToken);

        if (current is not null && current.IsUsable(now))
        {
            if (now < current.SendAgainAt)
            {
                return Answer(RequestCodeOutcome.AlreadySent, current, current.SendAgainAt);
            }

            if (await IsDeliveredAsync(current, cancellationToken))
            {
                return Answer(RequestCodeOutcome.AlreadyDelivered, current, current.ExpiresAt);
            }

            var limits = await CheckLimitsAsync(phoneNumber, now, cancellationToken);
            if (limits.IsFailure)
            {
                return Result.Failure<RequestCodeResponse>(limits.Error);
            }

            // Cannot fail: usable, and past SendAgainAt, both checked above with the same "now".
            current.SendAgain(now);

            return await SendAsync(current, isSendAgain: true, now, transaction);
        }

        var newCodeLimits = await CheckLimitsAsync(phoneNumber, now, cancellationToken);
        if (newCodeLimits.IsFailure)
        {
            return Result.Failure<RequestCodeResponse>(newCodeLimits.Error);
        }

        if (current is null)
        {
            current = SignInCode.Issue(phoneNumber, SignInCodeSeed.Generate(), now);
            db.SignInCodes.Add(current);
        }
        else
        {
            // Cannot fail: the code was found not usable above.
            current.Reissue(SignInCodeSeed.Generate(), now);
        }

        return await SendAsync(current, isSendAgain: false, now, transaction);
    }

    private async Task<Result> CheckLimitsAsync(string phoneNumber, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.LockGymSignInSendsAsync(cancellationToken);

        var anHourAgo = now - TimeSpan.FromHours(1);
        var phoneSends = await db.SignInCodeSends
            .CountAsync(send => send.PhoneNumber == phoneNumber && send.SentAt > anHourAgo, cancellationToken);

        var startOfToday = calendar.StartOfDayUtc(calendar.DayOf(now));
        var gymSends = await db.SignInCodeSends.CountAsync(send => send.SentAt >= startOfToday, cancellationToken);

        return SignInCode.CheckSendLimits(phoneSends, gymSends);
    }

    /// <summary>Asking costs nothing. No id, or no answer from Kavenegar, counts as not delivered.</summary>
    private async Task<bool> IsDeliveredAsync(SignInCode code, CancellationToken cancellationToken)
    {
        if (code.LastProviderMessageId is not { } messageId)
        {
            return false;
        }

        var deliveries = await account.GetDeliveriesAsync([messageId], cancellationToken);

        return deliveries.TryGetValue(messageId, out var delivery) && delivery == SmsDelivery.Delivered;
    }

    private async Task<Result<RequestCodeResponse>> SendAsync(
        SignInCode code,
        bool isSendAgain,
        DateTimeOffset now,
        IDbContextTransaction transaction)
    {
        // Not the request's token from here on: see the remarks.
        var message = new VerificationCodeMessage(code.PhoneNumber, codes.CodeFor(code.PhoneNumber, code.CodeSeed), code.MinutesLeft(now));
        var result = await sender.SendCodeAsync(message, CancellationToken.None);

        // Unknown: the request left and no answer came back, so the SMS may be on its way and paid
        // for. It is kept like a sent one: it counts toward the limits, and the code stands.
        if (result.Outcome is not (SmsSendOutcome.Sent or SmsSendOutcome.Unknown))
        {
            LogNotSent(logger, result.Outcome, result.ErrorCode);

            return Result.Failure<RequestCodeResponse>(SignInCodeErrors.NotSent);
        }

        code.RecordProviderMessage(result.ProviderMessageId);
        db.SignInCodeSends.Add(SignInCodeSend.Record(code.PhoneNumber, now, isSendAgain, result.ProviderMessageId, result.CostRial));

        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        return Answer(isSendAgain ? RequestCodeOutcome.SentAgain : RequestCodeOutcome.Sent, code, code.SendAgainAt);
    }

    private static Result<RequestCodeResponse> Answer(RequestCodeOutcome outcome, SignInCode code, DateTimeOffset sendAgainAt) =>
        new RequestCodeResponse(outcome, code.ExpiresAt, sendAgainAt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in code not sent: {Outcome}, provider code {ErrorCode}")]
    private static partial void LogNotSent(ILogger logger, SmsSendOutcome outcome, int? errorCode);
}
