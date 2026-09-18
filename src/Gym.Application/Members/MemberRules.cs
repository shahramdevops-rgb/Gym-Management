using FluentValidation;

using Gym.Domain.Members;

namespace Gym.Application.Members;

/// <summary>
/// The field checks create and update share, with the same error codes the entity uses, so the
/// form gets a field-level message before anything touches the database. The phone's format is
/// not checked here: that takes libphonenumber, and the handler does it through IPhoneNormalizer.
/// </summary>
public static class MemberRules
{
    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(MemberErrors.FullNameRequired.Code).WithMessage(MemberErrors.FullNameRequired.Description)
            .Must(name => name.Trim().Length <= Member.FullNameMaxLength)
            .WithErrorCode(MemberErrors.FullNameTooLong.Code).WithMessage(MemberErrors.FullNameTooLong.Description);

    public static IRuleBuilderOptions<T, string> ValidPhoneInput<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(phone => !string.IsNullOrWhiteSpace(phone))
            .WithErrorCode(MemberErrors.PhoneRequired.Code).WithMessage(MemberErrors.PhoneRequired.Description)

            // Anything longer is not a phone number, and libphonenumber need not be asked.
            .MaximumLength(30)
            .WithErrorCode(MemberErrors.PhoneInvalid.Code).WithMessage(MemberErrors.PhoneInvalid.Description);

    public static IRuleBuilderOptions<T, string?> ValidNotes<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Must(notes => notes is null || notes.Trim().Length <= Member.NotesMaxLength)
            .WithErrorCode(MemberErrors.NotesTooLong.Code).WithMessage(MemberErrors.NotesTooLong.Description);
}
