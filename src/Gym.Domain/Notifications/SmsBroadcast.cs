using Gym.Domain.Common;

namespace Gym.Domain.Notifications;

/// <summary>
/// One broadcast: the Owner's own text to a group of members (BUSINESS_RULES.md §10 <i>The
/// broadcast</i>, task 10.13). Each recipient's message is a <see cref="Notification"/> of the kind
/// <see cref="NotificationKind.Broadcast"/> that names this row.
/// </summary>
/// <remarks>
/// <para>
/// <b>The text kept is the Owner's middle only.</b> Each message puts the member's first name before
/// it and the opt-out line after it (<see cref="SmsText.ForBroadcast"/>), so continuing a stopped
/// broadcast writes the same message the others got.
/// </para>
/// <para>
/// <b>One at a time.</b> A partial unique index on the rows still <see cref="BroadcastStatus.Sending"/>
/// keeps a second one out, whatever the page or two tabs do.
/// </para>
/// <para>
/// <b>The counts are not kept here.</b> How many were sent, failed or cost what is read from its
/// messages, so it can never disagree with «پیامک‌ها».
/// </para>
/// </remarks>
public sealed class SmsBroadcast : Entity
{
    /// <summary>The most SMS parts any recipient's message may take (decided with the developer).</summary>
    public const int MaxParts = 3;

    /// <summary>
    /// The column's length. Three parts hold 201 characters, and the shortest name and the opt-out
    /// line take more than one of them, so a text the parts allow always fits.
    /// </summary>
    public const int TextMaxLength = 200;

    // For EF Core.
    private SmsBroadcast()
    {
    }

    /// <summary>The Owner's text, trimmed, its line breaks written <c>\n</c>.</summary>
    public string Text { get; private set; } = string.Empty;

    public BroadcastAudience Audience { get; private set; }

    public BroadcastStatus Status { get; private set; }

    /// <summary>Set only while <see cref="BroadcastStatus.Stopped"/>.</summary>
    public BroadcastStopReason? StopReason { get; private set; }

    /// <summary>A moment (UTC): when it finished or stopped; <c>null</c> while sending.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Postgres <c>xmin</c>: two tabs continuing it at once cannot both start its job.</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// A new broadcast, <see cref="BroadcastStatus.Sending"/>. Whether its text fits for the chosen
    /// recipients is <see cref="CheckLength"/>'s question, asked by the caller who knows them.
    /// </summary>
    public static Result<SmsBroadcast> Create(string text, BroadcastAudience audience)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!Enum.IsDefined(audience))
        {
            return Result.Failure<SmsBroadcast>(SmsBroadcastErrors.UnknownAudience);
        }

        var normalized = NormalizeText(text);
        if (normalized.Length == 0)
        {
            return Result.Failure<SmsBroadcast>(SmsBroadcastErrors.TextRequired);
        }

        if (normalized.Length > TextMaxLength)
        {
            return Result.Failure<SmsBroadcast>(SmsBroadcastErrors.TooLong);
        }

        return new SmsBroadcast
        {
            Text = normalized,
            Audience = audience,
            Status = BroadcastStatus.Sending,
        };
    }

    /// <summary>
    /// Trimmed at both ends, every line break written <c>\n</c>: a browser sends <c>\r\n</c>, which
    /// would count two characters on the phone for one break.
    /// </summary>
    public static string NormalizeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
    }

    /// <summary>
    /// <see cref="SmsBroadcastErrors.TooLong"/> when the message to the recipient with the longest
    /// first name would take more than <see cref="MaxParts"/> parts; <c>null</c> when it fits.
    /// </summary>
    /// <param name="normalizedText">The text through <see cref="NormalizeText"/>, not empty.</param>
    /// <param name="longestFirstName">The longest <see cref="SmsText.FirstName"/> among the recipients.</param>
    public static Error? CheckLength(string normalizedText, string longestFirstName)
    {
        ArgumentNullException.ThrowIfNull(longestFirstName);

        if (normalizedText.Length > TextMaxLength)
        {
            return SmsBroadcastErrors.TooLong;
        }

        return SmsParts.Count(SmsText.ForBroadcast(longestFirstName, normalizedText)) > MaxParts
            ? SmsBroadcastErrors.TooLong
            : null;
    }

    /// <summary>Every recipient has had their message written.</summary>
    public Result Finish(DateTimeOffset now)
    {
        if (Status != BroadcastStatus.Sending)
        {
            return Result.Failure(SmsBroadcastErrors.NotSending);
        }

        Status = BroadcastStatus.Finished;
        EndedAt = now;

        return Result.Success();
    }

    /// <summary>It stopped before everyone was written; the Owner can continue it.</summary>
    public Result Stop(BroadcastStopReason reason, DateTimeOffset now)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown stop reason.");
        }

        if (Status != BroadcastStatus.Sending)
        {
            return Result.Failure(SmsBroadcastErrors.NotSending);
        }

        Status = BroadcastStatus.Stopped;
        StopReason = reason;
        EndedAt = now;

        return Result.Success();
    }

    /// <summary>«ادامه ارسال»: sending again, to the members of its group who have no message of it yet.</summary>
    public Result Resume()
    {
        if (Status != BroadcastStatus.Stopped)
        {
            return Result.Failure(SmsBroadcastErrors.NotStopped);
        }

        Status = BroadcastStatus.Sending;
        StopReason = null;
        EndedAt = null;

        return Result.Success();
    }
}
