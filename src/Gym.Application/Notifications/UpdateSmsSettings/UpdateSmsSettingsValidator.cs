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
        RuleFor(command => command.LowSessions).Custom(CheckLowSessions);
        RuleFor(command => command.Birthday).Custom((settings, context) =>
            CheckKind(NotificationKind.Birthday, settings, context));
        RuleFor(command => command.PayableDue).Custom((settings, context) =>
            CheckKind(NotificationKind.PayableDue, settings, context));
        RuleFor(command => command.Absence).Custom((settings, context) =>
            CheckKind(NotificationKind.Absence, settings, context));
        RuleFor(command => command.DailyReport).Custom(CheckDailyReport);
        RuleFor(command => command.DebtReminder).Custom(CheckDebtReminder);

        // Nothing to fill in, but left out they would quietly turn the receipt, the welcome or the welcome back off.
        RuleFor(command => command.SubscriptionReceipt)
            .NotNull()
            .WithMessage("Every kind must be sent, on or off.")
            .WithErrorCode(SmsSettingsErrors.SettingsIncomplete.Code);
        RuleFor(command => command.Welcome)
            .NotNull()
            .WithMessage("Every kind must be sent, on or off.")
            .WithErrorCode(SmsSettingsErrors.SettingsIncomplete.Code);
        RuleFor(command => command.WelcomeBack)
            .NotNull()
            .WithMessage("Every kind must be sent, on or off.")
            .WithErrorCode(SmsSettingsErrors.SettingsIncomplete.Code);
        RuleFor(command => command.Broadcast)
            .NotNull()
            .WithMessage("Every kind must be sent, on or off.")
            .WithErrorCode(SmsSettingsErrors.SettingsIncomplete.Code);
    }

    private static void CheckDailyReport(DailyReportSettings? settings, ValidationContext<UpdateSmsSettingsCommand> context)
    {
        if (settings is null)
        {
            context.AddFailure(new ValidationFailure(context.PropertyPath, "Every kind must be sent, on or off.")
            {
                ErrorCode = SmsSettingsErrors.SettingsIncomplete.Code,
            });
            return;
        }

        Report(
            context,
            nameof(DailyReportSettings.Enabled),
            SmsSettings.CheckDailyReportComplete(settings.Enabled, settings.Phone));
        Report(context, nameof(DailyReportSettings.SendTime), SmsSettings.CheckDailyReportSendTime(settings.SendTime));
    }

    /// <summary>«جلسات رو به اتمام» (task 10.17): each refusal under its own box of the three rows.</summary>
    private static void CheckLowSessions(LowSessionsSettings? settings, ValidationContext<UpdateSmsSettingsCommand> context)
    {
        if (settings is null)
        {
            context.AddFailure(new ValidationFailure(context.PropertyPath, "Every kind must be sent, on or off.")
            {
                ErrorCode = SmsSettingsErrors.SettingsIncomplete.Code,
            });
            return;
        }

        Report(context, nameof(LowSessionsSettings.SmallPlanMax), SmsSettings.CheckLowSessionsSmallPlanMax(settings.SmallPlanMax));
        Report(
            context,
            nameof(LowSessionsSettings.MediumPlanMax),
            SmsSettings.CheckLowSessionsMediumPlanMax(settings.SmallPlanMax, settings.MediumPlanMax));
        Report(
            context,
            nameof(LowSessionsSettings.SmallThreshold),
            SmsSettings.CheckThreshold(NotificationKind.LowSessions, settings.SmallThreshold));
        Report(
            context,
            nameof(LowSessionsSettings.MediumThreshold),
            SmsSettings.CheckThreshold(NotificationKind.LowSessions, settings.MediumThreshold));
        Report(
            context,
            nameof(LowSessionsSettings.LargeThreshold),
            SmsSettings.CheckThreshold(NotificationKind.LowSessions, settings.LargeThreshold));
        Report(context, nameof(LowSessionsSettings.SendTime), SmsSettings.CheckSendTime(settings.SendTime));
        Report(context, nameof(LowSessionsSettings.Enabled), SmsSettings.CheckLowSessionsComplete(settings));
    }

    private static void CheckDebtReminder(DebtReminderSettings? settings, ValidationContext<UpdateSmsSettingsCommand> context)
    {
        if (settings is null)
        {
            context.AddFailure(new ValidationFailure(context.PropertyPath, "Every kind must be sent, on or off.")
            {
                ErrorCode = SmsSettingsErrors.SettingsIncomplete.Code,
            });
            return;
        }

        var kind = new SmsKindSettings(settings.Enabled, settings.DaysAfter, settings.SendTime);

        Report(
            context,
            nameof(DebtReminderSettings.DaysAfter),
            SmsSettings.CheckThreshold(NotificationKind.DebtReminder, settings.DaysAfter));
        Report(context, nameof(DebtReminderSettings.SendTime), SmsSettings.CheckSendTime(settings.SendTime));
        Report(context, nameof(DebtReminderSettings.MinAmount), SmsSettings.CheckDebtReminderMinAmount(settings.MinAmount));
        Report(
            context,
            nameof(DebtReminderSettings.Enabled),
            SmsSettings.CheckDebtReminderComplete(kind, settings.MinAmount));
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
