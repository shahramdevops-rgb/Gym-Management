using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Payments;
using Gym.Application.Payments.RegisterPayment;
using Gym.Application.ServiceCharges;
using Gym.Application.ServiceCharges.ChangeServiceChargeAmount;
using Gym.Application.ServiceCharges.RecordServiceCharge;
using Gym.Application.ServiceCharges.VoidServiceCharge;

namespace Gym.Api.Endpoints;

/// <summary>
/// Money owed for something used during a visit — today only هوازی (BUSINESS_RULES.md §7
/// <i>Gym services</i>). Front desk work throughout, so both roles.
/// </summary>
/// <remarks>
/// <para>
/// Recording is posted under the visit, because a charge belongs to one visit and there is
/// nothing to address until it exists — the same reasoning check-in is posted under the member.
/// Once it exists it has its own address.
/// </para>
/// <para>
/// Voiding is Staff or Owner, decided with the developer 1405/07/01, although §1's permissions
/// table puts voids with the Owner: the amount is typed at the desk and the desk has to be able
/// to take back its own mistake while the member is still standing there.
/// </para>
/// <para>
/// There is no refund endpoint here. A charge that needs correcting is voided with a reason, and
/// the void returns whatever was collected (§7); a smaller amount is then a fresh charge.
/// </para>
/// </remarks>
public static class ServiceChargesEndpoints
{
    public static IEndpointRouteBuilder MapServiceChargesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var visitCharges = app.MapGroup("/api/attendance/{attendanceId:guid}/service-charges")
            .WithTags("ServiceCharges")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        visitCharges.MapPost("/", async (Guid attendanceId, RecordServiceChargeCommand command, RecordServiceChargeHandler handler, CancellationToken ct) =>
                (await handler.Handle(attendanceId, command, ct))
                    .ToHttpResult(charge => Results.Created($"/api/service-charges/{charge.Id}", charge)))
            .AddEndpointFilter<ValidationFilter<RecordServiceChargeCommand>>()
            .WithName("RecordServiceCharge")
            .Produces<ServiceChargeResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var charges = app.MapGroup("/api/service-charges/{id:guid}")
            .WithTags("ServiceCharges")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        charges.MapPut("/amount", async (Guid id, ChangeServiceChargeAmountCommand command, ChangeServiceChargeAmountHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ChangeServiceChargeAmountCommand>>()
            .WithName("ChangeServiceChargeAmount")
            .Produces<ServiceChargeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        charges.MapPost("/void", async (Guid id, VoidServiceChargeCommand command, VoidServiceChargeHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<VoidServiceChargeCommand>>()
            .WithName("VoidServiceCharge")
            .Produces<ServiceChargeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        charges.MapPost("/payments", async (Guid id, RegisterPaymentCommand command, RegisterServiceChargePaymentHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct))
                    .ToHttpResult(payment => Results.Created($"/api/service-charges/{id}/payments/{payment.Id}", payment)))
            .AddEndpointFilter<ValidationFilter<RegisterPaymentCommand>>()
            .WithName("RegisterServiceChargePayment")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
