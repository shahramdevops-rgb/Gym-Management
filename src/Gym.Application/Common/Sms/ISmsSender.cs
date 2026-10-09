using Gym.Domain.Notifications;

namespace Gym.Application.Common.Sms;

/// <summary>
/// Sends one SMS as free text from the gym's dedicated line (BUSINESS_RULES.md §10 <i>Sending</i>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole text.</b> The wording is written by <c>SmsText</c> in the Domain; the sender only
/// carries it. (It was a template name and its values until Kavenegar refused the templates,
/// 1405/07/16.)
/// </para>
/// <para>
/// <b>It never throws for a failed send.</b> A busy provider, a refused number or a dropped
/// connection is an expected outcome of sending an SMS, so it comes back as a
/// <see cref="SmsSendResult"/> the caller records on the <c>Notification</c>. Only a bug throws.
/// </para>
/// <para>
/// <c>Sms:Provider</c> chooses the implementation: <c>FakeSmsSender</c>, which only logs, or
/// <c>KavenegarSmsSender</c> (task 10.4). Outside Production the real one only reaches the numbers in
/// <c>Sms:AllowedReceptors</c>. Tests always use the fake one.
/// </para>
/// </remarks>
public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

/// <param name="Receptor">The phone number, E.164, as a member's is stored.</param>
/// <param name="Text">The whole text, as the <c>Notification</c> keeps it.</param>
public sealed record SmsMessage(string Receptor, string Text);
