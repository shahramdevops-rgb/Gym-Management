namespace Gym.Application.SiteAuth.VerifyCode;

/// <param name="PhoneNumber">As typed, like <c>RequestCodeCommand</c>'s.</param>
/// <param name="Code">As typed: Persian or English digits.</param>
public sealed record VerifyCodeCommand(string PhoneNumber, string Code);
