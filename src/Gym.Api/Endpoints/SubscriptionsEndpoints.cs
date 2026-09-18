using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Subscriptions;
using Gym.Application.Subscriptions.AssignSubscription;
using Gym.Application.Subscriptions.GetSubscription;
using Gym.Application.Subscriptions.RenewSubscription;

namespace Gym.Api.Endpoints;

/// <summary>
/// Selling subscriptions. Front-desk work, so both roles (BUSINESS_RULES.md §1, permissions:
/// "Assign or renew subscriptions"). Freeze, unfreeze and cancel are Owner only (task 4.3).
/// </summary>
/// <remarks>
/// A sale belongs to a member, so it is posted under the member; once it exists, a subscription
/// has its own address.
/// </remarks>
public static class SubscriptionsEndpoints
{
    private const string Prefix = "/api/subscriptions";

    public static IEndpointRouteBuilder MapSubscriptionsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var sales = app.MapGroup("/api/members/{memberId:guid}/subscriptions")
            .WithTags("Subscriptions")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        sales.MapPost("/", async (Guid memberId, AssignSubscriptionCommand command, AssignSubscriptionHandler handler, CancellationToken ct) =>
                (await handler.Handle(memberId, command, ct)).ToHttpResult(Created))
            .AddEndpointFilter<ValidationFilter<AssignSubscriptionCommand>>()
            .WithName("AssignSubscription")
            .Produces<SubscriptionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        sales.MapPost("/renew", async (Guid memberId, RenewSubscriptionHandler handler, CancellationToken ct) =>
                (await handler.Handle(memberId, ct)).ToHttpResult(Created))
            .WithName("RenewSubscription")
            .Produces<SubscriptionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var subscriptions = app.MapGroup(Prefix)
            .WithTags("Subscriptions")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        subscriptions.MapGet("/{id:guid}", async (Guid id, GetSubscriptionHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetSubscription")
            .Produces<SubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult Created(SubscriptionResponse subscription) =>
        Results.Created($"{Prefix}/{subscription.Id}", subscription);
}
