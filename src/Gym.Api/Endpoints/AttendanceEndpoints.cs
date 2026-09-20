using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Application.Attendances;
using Gym.Application.Attendances.CheckIn;

namespace Gym.Api.Endpoints;

/// <summary>
/// Check-in. Front desk work, so both roles (BUSINESS_RULES.md §1, permissions:
/// "Check-in, check-out, cancel check-in").
/// </summary>
public static class AttendanceEndpoints
{
    private const string Prefix = "/api/members/{memberId:guid}/attendance";

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Attendance")
            .RequireAuthorization(Policies.StaffOrOwner)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/check-in", async (Guid memberId, CheckInHandler handler, CancellationToken ct) =>
                (await handler.Handle(memberId, ct)).ToHttpResult(attendance => Results.Created($"/api/attendance/{attendance.Id}", attendance)))
            .WithName("CheckIn")
            .Produces<AttendanceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
