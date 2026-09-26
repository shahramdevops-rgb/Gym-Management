using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Cafe;
using Gym.Application.Cafe.CreateCafeOrder;
using Gym.Application.Cafe.GetCafeOrder;
using Gym.Application.Payments;
using Gym.Application.Payments.RegisterPayment;

namespace Gym.Api.Endpoints;

/// <summary>
/// Cafe orders. Both roles (BUSINESS_RULES.md §1: "Register payments, create cafe orders", and
/// the cafe row the Owner widened when Phase 7 started).
/// </summary>
/// <remarks>
/// There is no endpoint to edit an order, and that is the rule rather than an omission: an order
/// is a financial record, so a mistake is cancelled with a reason and rung up again
/// (BUSINESS_RULES.md §8, §5). Cancelling arrives with task 7.3.
/// </remarks>
public static class CafeOrdersEndpoints
{
    private const string Prefix = "/api/cafe/orders";

    public static IEndpointRouteBuilder MapCafeOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Cafe")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
                CreateCafeOrderCommand command,
                CreateCafeOrderHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(command, ct))
                        .ToHttpResult(order => Results.Created($"{Prefix}/{order.Id}", order)))
            .AddEndpointFilter<ValidationFilter<CreateCafeOrderCommand>>()
            .WithName("CreateCafeOrder")
            .Produces<CafeOrderResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}", async (Guid id, GetCafeOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetCafeOrder")
            .Produces<CafeOrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Settling an order left on a member's account, in as many instalments as it takes
        // (BUSINESS_RULES.md §5). The same command as every other payment.
        group.MapPost("/{id:guid}/payments", async (
                Guid id,
                RegisterPaymentCommand command,
                RegisterCafeOrderPaymentHandler handler,
                CancellationToken ct) =>
                    (await handler.Handle(id, command, ct))
                        .ToHttpResult(payment => Results.Created($"/api/payments/{payment.Id}", payment)))
            .AddEndpointFilter<ValidationFilter<RegisterPaymentCommand>>()
            .WithName("RegisterCafeOrderPayment")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
