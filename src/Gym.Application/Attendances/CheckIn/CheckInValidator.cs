using FluentValidation;

using Gym.Domain.Attendances;
using Gym.Domain.Subscriptions;

namespace Gym.Application.Attendances.CheckIn;

/// <summary>
/// The shape of the optional sale. A plan's sessions follow the same limits as selling one from the
/// profile, under the same error codes, and are reported as <c>sessionCount</c> so the sale form in
/// the check-in box shows them under their field.
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
                // A missing count carries the same code as too few: the form has one message per
                // field, not one per kind of mistake.
                RuleFor(command => command.Sale!.SessionCount)
                    .Must(sessions => sessions >= Subscription.MinSessionCount)
                    .OverridePropertyName("sessionCount")
                    .WithErrorCode(SubscriptionErrors.SessionCountTooLow.Code)
                    .WithMessage(SubscriptionErrors.SessionCountTooLow.Description);

                RuleFor(command => command.Sale!.SessionCount)
                    .Must(sessions => sessions is null || sessions <= Subscription.MaxSessionCount)
                    .OverridePropertyName("sessionCount")
                    .WithErrorCode(SubscriptionErrors.SessionCountTooHigh.Code)
                    .WithMessage(SubscriptionErrors.SessionCountTooHigh.Description);
            });

            // A single visit is always one day and one session (§4); a session count sent with it
            // means the caller thinks it is selling something else.
            When(command => command.Sale!.Kind == CheckInSaleKind.SingleVisit, () =>
            {
                RuleFor(command => command.Sale)
                    .Must(sale => sale!.SessionCount is null)
                    .WithErrorCode(AttendanceErrors.SaleInvalid.Code)
                    .WithMessage(AttendanceErrors.SaleInvalid.Description);
            });
        });
    }
}
