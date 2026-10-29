using System.Security.Cryptography;
using System.Text;

using Gym.Domain.Common;

namespace Gym.Domain.SiteAuth;

/// <summary>
/// The one-time code a phone signs in to the site with (BUSINESS_RULES.md §15 <i>The one-time
/// code</i>). One row per phone: the code it holds now, or held last.
/// </summary>
/// <remarks>
/// <para>
/// <b>The code itself is not stored.</b> The row keeps a random <see cref="CodeSeed"/>, and the code
/// is computed from it with a secret that lives in the server's configuration, not in the database
/// (<c>ISignInCodes</c>). The same seed always gives the same code, which is what lets «ارسال دوباره»
/// send the same code again; a database backup alone gives no code at all.
/// </para>
/// <para>
/// <b>A new code reuses the row.</b> When the old one has expired, been used or had its 5 wrong
/// tries, <see cref="Reissue"/> gives the row a new seed and starts the clock and the tries again.
/// One row per phone is what the unique index on <see cref="PhoneNumber"/> holds the database to.
/// </para>
/// </remarks>
public sealed class SignInCode : Entity
{
    /// <summary>The code's length: 5 digits, leading zeros included.</summary>
    public const int Digits = 5;

    public const int MaxWrongTries = 5;

    /// <summary>Code SMS one phone may be sent in any hour, sends again included.</summary>
    public const int MaxSendsPerPhonePerHour = 5;

    /// <summary>Code SMS the whole gym may send in one of its days, so a flood cannot empty the credit.</summary>
    public const int MaxSendsPerGymDay = 300;

    /// <summary>
    /// Wrong tries, over all of a phone's codes in one of the gym's days, that close the phone until
    /// the next day (BUSINESS_RULES.md §15). Three codes' worth: a slip of the fingers never reaches it.
    /// </summary>
    public const int MaxWrongTriesPerDay = 15;

    /// <summary>How long a code is valid for, from the moment it is made. Sends again do not extend it.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    /// <summary>How long after a send «ارسال دوباره» is offered.</summary>
    public static readonly TimeSpan SendAgainAfter = TimeSpan.FromMinutes(2);

    // For EF Core.
    private SignInCode()
    {
    }

    /// <summary>E.164, for example <c>+989121234567</c>, as a member's is stored. Unique.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>The random value the code is computed from. Never written to the audit log.</summary>
    public string CodeSeed { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>The last time this code went out by SMS, first send or send again.</summary>
    public DateTimeOffset LastSentAt { get; private set; }

    /// <summary>
    /// Kavenegar's id for the last send, to ask whether it was delivered; <c>null</c> when the
    /// provider gave none (no answer came back).
    /// </summary>
    public long? LastProviderMessageId { get; private set; }

    public int WrongTries { get; private set; }

    /// <summary>When the code signed the phone in. A used code never works again.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>
    /// The gym's day of the last wrong try, and how many wrong tries the phone made that day. Kept
    /// across new codes in this row, so stopping at 4 tries a code and waiting for a new one does
    /// not escape the day's count.
    /// </summary>
    public DateOnly? WrongTriesOn { get; private set; }

    public int WrongTriesThatDay { get; private set; }

    /// <summary>
    /// Postgres <c>xmin</c>. The handlers also lock the phone first; this is the database's own
    /// guard against two saves built on the same read.
    /// </summary>
    public uint Version { get; private set; }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && WrongTries < MaxWrongTries && now < ExpiresAt;

    /// <summary>
    /// Whether the phone is closed for <paramref name="today"/>: 15 wrong tries that day, over all its
    /// codes (BUSINESS_RULES.md §15). No new code is sent, and the current one no longer works.
    /// </summary>
    public bool IsClosedOn(DateOnly today) => WrongTriesOn == today && WrongTriesThatDay >= MaxWrongTriesPerDay;

    /// <summary>When «ارسال دوباره» may send this code again.</summary>
    public DateTimeOffset SendAgainAt => LastSentAt + SendAgainAfter;

    /// <summary>Minutes the code has left, rounded up: what the SMS says (<c>%token2</c>).</summary>
    public int MinutesLeft(DateTimeOffset now) =>
        Math.Max(0, (int)Math.Ceiling((ExpiresAt - now).TotalMinutes));

    /// <summary>The first code for a phone.</summary>
    public static SignInCode Issue(string phoneNumber, string codeSeed, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        var code = new SignInCode { PhoneNumber = phoneNumber };
        code.Start(codeSeed, now);

        return code;
    }

    /// <summary>
    /// A new code in this phone's row. Refused while the current one still works: the phone is
    /// then answered with the code it already has (§15: no new SMS while a code is valid).
    /// </summary>
    /// <param name="today">The gym's day: a phone closed for it gets no new code.</param>
    public Result Reissue(string codeSeed, DateTimeOffset now, DateOnly today)
    {
        if (IsUsable(now))
        {
            return Result.Failure(SignInCodeErrors.StillValid);
        }

        if (IsClosedOn(today))
        {
            return Result.Failure(SignInCodeErrors.PhoneClosedForToday);
        }

        Start(codeSeed, now);

        return Result.Success();
    }

    /// <summary>
    /// «ارسال دوباره»: the same code goes out again. Only while it still works, and not before
    /// <see cref="SendAgainAt"/>. Whether Kavenegar already delivered it is asked by the caller,
    /// which has the provider.
    /// </summary>
    public Result SendAgain(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            return Result.Failure(SignInCodeErrors.Expired);
        }

        if (now < SendAgainAt)
        {
            return Result.Failure(SignInCodeErrors.SendAgainTooSoon);
        }

        LastSentAt = now;
        LastProviderMessageId = null;

        return Result.Success();
    }

    /// <summary>Kavenegar's id for the send just made, once it answers.</summary>
    public void RecordProviderMessage(long? providerMessageId) => LastProviderMessageId = providerMessageId;

    /// <summary>
    /// Checks a typed code against the one this row stands for. A wrong one costs a try of the code
    /// and of the phone's day: the fifth ends the code, the day's fifteenth closes the phone until
    /// tomorrow. The right one is used up, so it works only once.
    /// </summary>
    /// <param name="typed">What the person typed, digits already in English.</param>
    /// <param name="expected">The code computed from <see cref="CodeSeed"/>.</param>
    /// <param name="today">The gym's day, for the day's count of wrong tries.</param>
    public Result Verify(string typed, string expected, DateTimeOffset now, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentException.ThrowIfNullOrWhiteSpace(expected);

        // Before anything else: once closed, even the right code is not looked at today.
        if (IsClosedOn(today))
        {
            return Result.Failure(SignInCodeErrors.PhoneClosedForToday);
        }

        if (!IsUsable(now))
        {
            return Result.Failure(SignInCodeErrors.Expired);
        }

        // In constant time, so how long the answer takes says nothing about how many digits were right.
        var matches = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(typed), Encoding.ASCII.GetBytes(expected));
        if (!matches)
        {
            WrongTries++;
            WrongTriesThatDay = WrongTriesOn == today ? WrongTriesThatDay + 1 : 1;
            WrongTriesOn = today;

            if (IsClosedOn(today))
            {
                return Result.Failure(SignInCodeErrors.PhoneClosedForToday);
            }

            return Result.Failure(WrongTries < MaxWrongTries ? SignInCodeErrors.Wrong : SignInCodeErrors.TooManyWrongTries);
        }

        UsedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// The limits on paid SMS, checked before Kavenegar is called: per phone in the last hour and
    /// for the whole gym in its day.
    /// </summary>
    public static Result CheckSendLimits(int phoneSendsInLastHour, int gymSendsToday)
    {
        if (gymSendsToday >= MaxSendsPerGymDay)
        {
            return Result.Failure(SignInCodeErrors.GymDailyLimitReached);
        }

        return phoneSendsInLastHour >= MaxSendsPerPhonePerHour
            ? Result.Failure(SignInCodeErrors.PhoneHourlyLimitReached)
            : Result.Success();
    }

    private void Start(string codeSeed, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeSeed);

        CodeSeed = codeSeed;
        IssuedAt = now;
        ExpiresAt = now + Lifetime;
        LastSentAt = now;
        LastProviderMessageId = null;
        WrongTries = 0;
        UsedAt = null;
    }
}
