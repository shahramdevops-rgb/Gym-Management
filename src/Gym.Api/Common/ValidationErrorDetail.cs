namespace Gym.Api.Common;

/// <summary>
/// One failed validation rule for one field.
/// </summary>
/// <param name="Code">
/// The stable contract, e.g. <c>Members.PhoneRequired</c>. The Persian frontend maps it to a
/// message in <c>web/src/lib/errors.ts</c>, so rewording a validator can never change what a
/// user reads. FluentValidation supplies its own code (<c>NotEmptyValidator</c>) unless the
/// rule sets one with <c>.WithErrorCode(...)</c>.
/// </param>
/// <param name="Description">The English message, for developers, logs and as a fallback.</param>
public sealed record ValidationErrorDetail(string Code, string Description);
