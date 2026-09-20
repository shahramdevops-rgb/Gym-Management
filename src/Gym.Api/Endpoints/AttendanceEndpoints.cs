using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Attendances;
using Gym.Application.Attendances.CancelCheckIn;
using Gym.Application.Attendances.CheckIn;
using Gym.Application.Attendances.CheckOut;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Attendances.ListMemberAttendance;
using Gym.Application.Common.Paging;

namespace Gym.Api.Endpoints;

/// <summary>
/// Check-in, check-out, cancel check-in and the front desk's lists (BUSINESS_RULES.md §7). Front
/// desk work, so both roles (§1, permissions: "Check-in, check-out, cancel check-in").
/// </summary>
/// <remarks>
/// Check-in is posted under the member, because there is nothing to address until it exists
/// (task 5.2). Once an attendance exists it has its own address, the same reasoning
/// <c>SubscriptionsEndpoints</c> uses for freeze/unfreeze/cancel.
/// </remarks>
public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var memberAttendance = app.MapGroup("/api/members/{memberId:guid}/attendance")
            .WithTags("Attendance")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        memberAttendance.MapPost("/check-in", async (Guid memberId, CheckInHandler handler, CancellationToken ct) =>
                (await handler.Handle(memberId, ct)).ToHttpResult(attendance => Results.Created($"/api/attendance/{attendance.Id}", attendance)))
            .WithName("CheckIn")
            .Produces<AttendanceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        memberAttendance.MapGet("/", async (Guid memberId, [AsParameters] ListMemberAttendanceQuery query, ListMemberAttendanceHandler handler, CancellationToken ct) =>
                (await handler.Handle(memberId, query, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<ListMemberAttendanceQuery>>()
            .WithName("ListMemberAttendance")
            .Produces<PagedResponse<AttendanceResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var attendance = app.MapGroup("/api/attendance")
            .WithTags("Attendance")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        attendance.MapPost("/{id:guid}/check-out", async (Guid id, CheckOutHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("CheckOut")
            .Produces<AttendanceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        attendance.MapPost("/{id:guid}/cancel", async (Guid id, CancelCheckInHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("CancelCheckIn")
            .Produces<AttendanceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        attendance.MapGet("/currently-inside", async ([AsParameters] ListCurrentlyInsideQuery query, ListCurrentlyInsideHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(query, ct)))
            .AddEndpointFilter<ValidationFilter<ListCurrentlyInsideQuery>>()
            .WithName("ListCurrentlyInside")
            .Produces<PagedResponse<CurrentlyInsideResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
