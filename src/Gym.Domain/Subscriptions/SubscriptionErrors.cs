using Gym.Domain.Common;

namespace Gym.Domain.Subscriptions;

public static class SubscriptionErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Subscriptions.NotFound",
        "No subscription has that id.");

    public static readonly Error PlanRequired = Error.Validation(
        "Subscriptions.PlanRequired",
        "A plan is required.");

    /// <summary>Renew sells the plan of the member's latest subscription; there is none.</summary>
    public static readonly Error NothingToRenew = Error.BusinessRule(
        "Subscriptions.NothingToRenew",
        "The member has no subscription to renew.");

    /// <summary>
    /// Another sale or change for the same member was saved at the same moment. Trying again
    /// reads the new state and schedules correctly.
    /// </summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Subscriptions.ChangedConcurrently",
        "The member's subscriptions were changed by someone else at the same moment. Try again.");

    // One error per status that blocks using a subscription, so the front desk can say why:
    // "starts on …", "expired", "no sessions left", "frozen" and "cancelled" are different
    // conversations with the member.

    public static readonly Error NotStarted = Error.BusinessRule(
        "Subscriptions.NotStarted",
        "The subscription has not started yet.");

    public static readonly Error Expired = Error.BusinessRule(
        "Subscriptions.Expired",
        "The subscription has expired.");

    public static readonly Error NoSessionsLeft = Error.BusinessRule(
        "Subscriptions.NoSessionsLeft",
        "The subscription has no sessions left.");

    public static readonly Error Frozen = Error.BusinessRule(
        "Subscriptions.Frozen",
        "The subscription is frozen.");

    public static readonly Error Cancelled = Error.BusinessRule(
        "Subscriptions.Cancelled",
        "The subscription is cancelled.");

    /// <summary>
    /// BUSINESS_RULES.md §4: the member used every session on the day the subscription started
    /// and renewed the same day, so the renewal cannot cover today as well and starts tomorrow.
    /// Its own status only says "not started yet", which is not the answer the front desk needs.
    /// </summary>
    public static readonly Error NextStartsTomorrow = Error.BusinessRule(
        "Subscriptions.NextStartsTomorrow",
        "Today's sessions are used up and the next subscription starts tomorrow.");

    public static readonly Error NotFrozen = Error.BusinessRule(
        "Subscriptions.NotFrozen",
        "The subscription is not frozen.");

    /// <summary>BUSINESS_RULES.md §4, freeze: every allowed freeze day has been used.</summary>
    public static readonly Error FreezeLimitReached = Error.BusinessRule(
        "Subscriptions.FreezeLimitReached",
        "The subscription has used all of its freeze days.");

    public static readonly Error CancelReasonRequired = Error.Validation(
        "Subscriptions.CancelReasonRequired",
        "A reason is required to cancel a subscription.");

    public static readonly Error CancelReasonTooLong = Error.Validation(
        "Subscriptions.CancelReasonTooLong",
        "The cancellation reason is too long.");
}
