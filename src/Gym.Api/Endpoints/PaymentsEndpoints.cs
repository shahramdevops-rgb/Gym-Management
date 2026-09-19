using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Payments;
using Gym.Application.Payments.RegisterPayment;

namespace Gym.Api.Endpoints;

/// <summary>
/// Registering payments against a subscription (BUSINESS_RULES.md §5). Front-desk work, so both
/// roles (§1, permissions: "Register payments, create cafe orders"). Refunds are task 4.5.
/// </summary>
public static class PaymentsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var payments = app.MapGroup("/api/subscriptions/{subscriptionId:guid}/payments")
            .WithTags("Payments")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        payments.MapPost("/", async (Guid subscriptionId, RegisterPaymentCommand command, RegisterPaymentHandler handler, CancellationToken ct) =>
                (await handler.Handle(subscriptionId, command, ct)).ToHttpResult(payment => Created(subscriptionId, payment)))
            .AddEndpointFilter<ValidationFilter<RegisterPaymentCommand>>()
            .WithName("RegisterPayment")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static IResult Created(Guid subscriptionId, PaymentResponse payment) =>
        Results.Created($"/api/subscriptions/{subscriptionId}/payments/{payment.Id}", payment);
}
