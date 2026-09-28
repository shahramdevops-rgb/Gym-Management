using FluentValidation;

using Gym.Domain.Attendances;
using Gym.Domain.Subscriptions;

namespace Gym.Application.Attendances.CheckIn;

/// <summary>
/// The shape of the optional sale. A plan's numbers follow the same limits as selling one from the
/// profile, under the same error codes, and are reported as <c>durationDays</c> and
/// <c>sessionCount</c> so the sale form in the check-in box shows each one under its own field.
/// </summary>
public sealed class CheckInValidator : AbstractValidator<CheckInCommand>
{
    public CheckInValidator()
    {
        When(command => command.Sale is not null, () =>
        {
            RuleFor(command => command.Sale!.Kind)
                .IsInEnum()
                .WithErrorCode(AttendanceErrors.SaleInvalid.Code)
                .WithMessage(AttendanceErrors.SaleInvalid.Description);

            When(command => command.Sale!.Kind == CheckInSaleKind.Membership, () =>
            {
                // One rule each, missing included, so a missing number carries the same code as a
                // wrong one: the form has one message per field, not one per kind of mistake.
                RuleFor(command => command.Sale!.DurationDays)
                    .Must(days => days is >= 1 and <= Subscription.MaxDurationDays)
                    .OverridePropertyName("durationDays")
                    .WithErrorCode(SubscriptionErrors.DurationInvalid.Code)
                    .WithMessage(SubscriptionErrors.DurationInvalid.Description);

                RuleFor(command => command.Sale!.SessionCount)
                    .Must(sessions => sessions >= Subscription.MinSessionCount)
                    .OverridePropertyName("sessionCount")
                    .WithErrorCode(SubscriptionErrors.SessionCountTooLow.Code)
                    .WithMessage(SubscriptionErrors.SessionCountTooLow.Description);
            });

            // A single visit is always one day and one session (§4); numbers sent with it mean the
            // caller thinks it is selling something else.
            When(command => command.Sale!.Kind == CheckInSaleKind.SingleVisit, () =>
            {
                RuleFor(command => command.Sale)
                    .Must(sale => sale!.DurationDays is null && sale.SessionCount is null)
                    .WithErrorCode(AttendanceErrors.SaleInvalid.Code)
                    .WithMessage(AttendanceErrors.SaleInvalid.Description);
            });
        });
    }
}
