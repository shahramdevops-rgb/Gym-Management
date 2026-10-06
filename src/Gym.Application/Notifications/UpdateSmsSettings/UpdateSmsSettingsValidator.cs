using FluentValidation;
using FluentValidation.Results;

using Gym.Domain.Common;
using Gym.Domain.Notifications;

namespace Gym.Application.Notifications.UpdateSmsSettings;

/// <summary>
/// The checks from <see cref="SmsSettings"/>, one per field, so the page shows each refusal under the
/// field it belongs to (<c>birthday.threshold</c>, <c>payableDue.enabled</c>) and the validator cannot
/// disagree with the entity. The Owner's number is checked by the handler, which needs libphonenumber.
/// </summary>
/// <remarks>
/// One custom rule per kind rather than a child validator class: the assembly scan registers every
/// validator it finds, and a child that needs the kind in its constructor cannot be built by DI.
/// </remarks>
public sealed class UpdateSmsSettingsValidator : AbstractValidator<UpdateSmsSettingsCommand>
{
    public UpdateSmsSettingsValidator()
    {
        RuleFor(command => command.SubscriptionExpiring).Custom((settings, context) =>
            CheckKind(NotificationKind.SubscriptionExpiring, settings, context));
        RuleFor(command => command.LowSessions).Custom((settings, context) =>
            CheckKind(NotificationKind.LowSessions, settings, context));
        RuleFor(command => command.Birthday).Custom((settings, context) =>
            CheckKind(NotificationKind.Birthday, settings, context));
        RuleFor(command => command.PayableDue).Custom((settings, context) =>
            CheckKind(NotificationKind.PayableDue, settings, context));
    }

    private static void CheckKind(
        NotificationKind kind, SmsKindSettings? settings, ValidationContext<UpdateSmsSettingsCommand> context)
    {
        if (settings is null)
        {
            // A client that skipped part of the page; refused rather than read as "off".
            context.AddFailure(new ValidationFailure(context.PropertyPath, "Every kind must be sent, on or off.")
            {
                ErrorCode = SmsSettingsErrors.SettingsIncomplete.Code,
            });
            return;
        }

        Report(context, nameof(SmsKindSettings.Threshold), SmsSettings.CheckThreshold(kind, settings.Threshold));
        Report(context, nameof(SmsKindSettings.SendTime), SmsSettings.CheckSendTime(settings.SendTime));
        Report(context, nameof(SmsKindSettings.TemplateName), SmsSettings.CheckTemplateName(settings.TemplateName));

        // Under the switch: it is the switch that cannot be on yet.
        Report(
            context,
            nameof(SmsKindSettings.Enabled),
            SmsSettings.CheckComplete(kind, settings, context.InstanceToValidate.OwnerPhone));
    }

    private static void Report(ValidationContext<UpdateSmsSettingsCommand> context, string field, Error? error)
    {
        if (error is null)
        {
            return;
        }

        context.AddFailure(new ValidationFailure($"{context.PropertyPath}.{field}", error.Description)
        {
            ErrorCode = error.Code,
        });
    }
}
