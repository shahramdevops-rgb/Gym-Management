using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Payments;
using Gym.Application.Payments.ListMemberPayments;
using Gym.Application.Payments.RegisterPayment;
using Gym.Application.Payments.RegisterRefund;

namespace Gym.Api.Endpoints;

/// <summary>
/// Payments and refunds (BUSINESS_RULES.md §5). Registering a payment is front-desk work, so both
/// roles; refunds and void are Owner only (§1, permissions table).
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
                (await handler.Handle(subscriptionId, command, ct)).ToHttpResult(payment => Created(subscriptionId, "payments", payment)))
            .AddEndpointFilter<ValidationFilter<RegisterPaymentCommand>>()
            .WithName("RegisterPayment")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // Owner only (BUSINESS_RULES.md §1, permissions: "Refunds, voids ...").
        var refunds = app.MapGroup("/api/subscriptions/{subscriptionId:guid}/refunds")
            .WithTags("Payments")
            .RequireAuthorization(Policies.OwnerOnly)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        refunds.MapPost("/", async (Guid subscriptionId, RegisterRefundCommand command, RegisterRefundHandler handler, CancellationToken ct) =>
                (await handler.Handle(subscriptionId, command, ct)).ToHttpResult(payment => Created(subscriptionId, "refunds", payment)))
            .AddEndpointFilter<ValidationFilter<RegisterRefundCommand>>()
            .WithName("RegisterRefund")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var memberPayments = app.MapGroup("/api/members/{memberId:guid}/payments")
            .WithTags("Payments")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        memberPayments.MapGet("/", async (Guid memberId, [AsParameters] ListMemberPaymentsQuery query, ListMemberPaymentsHandler handler, CancellationToken ct) =>
                (await handler.Handle(memberId, query, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ListMemberPaymentsQuery>>()
            .WithName("ListMemberPayments")
            .Produces<PagedResponse<PaymentHistoryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult Created(Guid subscriptionId, string segment, PaymentResponse payment) =>
        Results.Created($"/api/subscriptions/{subscriptionId}/{segment}/{payment.Id}", payment);
}
