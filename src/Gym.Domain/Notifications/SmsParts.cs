namespace Gym.Domain.Notifications;

/// <summary>
/// How many SMS parts a text takes, each paid for (BUSINESS_RULES.md §10 <i>Sending</i>): a Persian
/// SMS holds 70 characters in one part and 67 in each part of a longer one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Always the Persian count.</b> A text with any character outside the GSM alphabet is sent as
/// UCS-2, and every text the gym sends has Persian in it («عزیز», «لغو11»), so the Latin count
/// (160 and 153) never applies.
/// </para>
/// <para>
/// <b>Characters are UTF-16 code units</b>, as <see cref="string.Length"/> counts them and as UCS-2
/// sends them: an emoji takes two. The frontend counts the same way (<c>smsParts</c> in
/// <c>web/src/features/sms</c>), with the same cases in its tests.
/// </para>
/// </remarks>
public static class SmsParts
{
    public const int SinglePartLength = 70;

    public const int MultiPartLength = 67;

    public static int Count(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length <= SinglePartLength)
        {
            return 1;
        }

        return (text.Length + MultiPartLength - 1) / MultiPartLength;
    }
}
