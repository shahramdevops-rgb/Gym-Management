using Gym.Domain.Notifications;

namespace Gym.Application.Common.Sms;

/// <summary>
/// Sends one SMS through the provider's template method (BUSINESS_RULES.md §10 <i>Sending</i>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Template-first.</b> It takes a template name and the values for its blanks, never a finished
/// text: the wording lives in the provider's panel. If the birthday greeting is ever refused as a
/// template (§0), a free-text method joins this one here; nothing that calls it changes.
/// </para>
/// <para>
/// <b>It never throws for a failed send.</b> A busy provider, a refused template or a dropped
/// connection is an expected outcome of sending an SMS, so it comes back as a
/// <see cref="SmsSendResult"/> the caller records on the <c>Notification</c>. Only a bug throws.
/// </para>
/// <para>
/// <c>Sms:Provider</c> chooses the implementation: <c>FakeSmsSender</c>, which only logs, or the
/// real provider (task 10.4). Tests always use the fake one.
/// </para>
/// </remarks>
public interface ISmsSender
{
    Task<SmsSendResult> SendTemplateAsync(SmsTemplateMessage message, CancellationToken cancellationToken);
}

/// <param name="Receptor">The phone number, E.164, as a member's is stored.</param>
/// <param name="Template">The template's name in the provider's panel.</param>
public sealed record SmsTemplateMessage(string Receptor, string Template, SmsTokens Tokens);
