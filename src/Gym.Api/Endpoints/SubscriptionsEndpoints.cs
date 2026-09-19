using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Subscriptions;
using Gym.Application.Subscriptions.AssignSubscription;
using Gym.Application.Subscriptions.CancelSubscription;
using Gym.Application.Subscriptions.FreezeSubscription;
using Gym.Application.Subscriptions.GetSubscription;
using Gym.Application.Subscriptions.ListMemberSubscriptions;
using Gym.Application.Subscriptions.RenewSubscription;
using Gym.Application.Subscriptions.UnfreezeSubscription;

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

        // The member's subscription history, newest first (task 4.5).
        sales.MapGet("/", async (Guid memberId, [AsParameters] ListMemberSubscriptionsQuery query, ListMemberSubscriptionsHandler handler, CancellationToken ct) =>
                (await handler.Handle(memberId, query, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ListMemberSubscriptionsQuery>>()
            .WithName("ListMemberSubscriptions")
            .Produces<PagedResponse<SubscriptionResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

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

        // Owner only (BUSINESS_RULES.md §1, permissions: "Freeze, unfreeze, cancel subscriptions").
        var ownerActions = app.MapGroup(Prefix)
            .WithTags("Subscriptions")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        ownerActions.MapPost("/{id:guid}/freeze", async (Guid id, FreezeSubscriptionHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("FreezeSubscription")
            .Produces<SubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        ownerActions.MapPost("/{id:guid}/unfreeze", async (Guid id, UnfreezeSubscriptionHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("UnfreezeSubscription")
            .Produces<SubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        ownerActions.MapPost("/{id:guid}/cancel", async (Guid id, CancelSubscriptionCommand command, CancelSubscriptionHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<CancelSubscriptionCommand>>()
            .WithName("CancelSubscription")
            .Produces<SubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static IResult Created(SubscriptionResponse subscription) =>
        Results.Created($"{Prefix}/{subscription.Id}", subscription);
}
