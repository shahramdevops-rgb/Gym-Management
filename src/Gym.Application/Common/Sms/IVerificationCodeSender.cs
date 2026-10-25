namespace Gym.Application.Common.Sms;

/// <summary>
/// Sends a one-time code as Kavenegar's verification template (BUSINESS_RULES.md §15): not free
/// text from the gym's line like <see cref="ISmsSender"/>, but the template <c>gymExpiring</c>,
/// which Kavenegar sends from its shared line.
/// </summary>
/// <remarks>
/// Like <see cref="ISmsSender"/>, it never throws for a failed send, and outside Production it only
/// reaches the numbers in <c>Sms:AllowedReceptors</c>. Nothing that implements it may write the code
/// to a log.
/// </remarks>
public interface IVerificationCodeSender
{
    Task<SmsSendResult> SendCodeAsync(VerificationCodeMessage message, CancellationToken cancellationToken);
}

/// <param name="Receptor">The phone number, E.164.</param>
/// <param name="Code">The code: the template's <c>%token</c>.</param>
/// <param name="MinutesLeft">The minutes it is still valid for: the template's <c>%token2</c>.</param>
public sealed record VerificationCodeMessage(string Receptor, string Code, int MinutesLeft)
{
    /// <summary>Without the code: a record prints every property, and this one may end up in a log.</summary>
    public override string ToString() => $"VerificationCodeMessage {{ Receptor = {Receptor}, MinutesLeft = {MinutesLeft} }}";
}
