using System.Security.Claims;

using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Bookings;
using Gym.Application.Bookings.CheckStudentSchedule;
using Gym.Application.Bookings.EndStudentSchedule;
using Gym.Application.Bookings.GetMyCancellations;
using Gym.Application.Bookings.GetMySessions;
using Gym.Application.Bookings.GetSessionStudents;
using Gym.Application.Bookings.GetStudentAttendance;
using Gym.Application.Bookings.GetStudentSchedule;
using Gym.Application.Bookings.GetTrainerDay;
using Gym.Application.Bookings.LeaveMySession;
using Gym.Application.Bookings.MoveMySession;
using Gym.Application.Bookings.RemoveSessionStudent;
using Gym.Application.Bookings.ReturnToMySession;
using Gym.Application.Bookings.SeeMyCancellations;
using Gym.Application.Bookings.SetStudentSchedule;

namespace Gym.Api.Endpoints;

/// <summary>
/// Students in the trainer's sessions on the site (BUSINESS_RULES.md §15 <i>Students in sessions</i>,
/// task 15.6b): the trainer's «برنامهٔ ثابت» of each student and «شاگردان سانس» behind
/// <see cref="Policies.SiteTrainer"/>, the member's «سانس‌های من» behind <see cref="Policies.SiteMember"/>.
/// No path names the trainer: it is always the signed-in account. The handlers read the roles again
/// from the database, since a token's may be 15 minutes old.
/// </summary>
public static class SiteBookingEndpoints
{
    public static IEndpointRouteBuilder MapSiteBookingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var schedule = app.MapGroup($"{SitePaths.Root}/coaching/students/{{memberId:guid}}/schedule")
            .WithTags("Site bookings")
            .RequireAuthorization(Policies.SiteTrainer)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        schedule.MapGet("/", async (Guid memberId, ClaimsPrincipal user, GetStudentScheduleHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), memberId, ct)).ToHttpResult())
            .WithName("GetStudentSchedule")
            .Produces<StudentScheduleResponse>();

        schedule.MapPut("/", async (Guid memberId, ClaimsPrincipal user, SetStudentScheduleCommand command, SetStudentScheduleHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), memberId, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<SetStudentScheduleCommand>>()
            .WithName("SetStudentSchedule")
            .Produces<StudentScheduleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // A read that takes a body: what the save would do, with nothing kept.
        schedule.MapPost("/check", async (Guid memberId, ClaimsPrincipal user, SetStudentScheduleCommand command, CheckStudentScheduleHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), memberId, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<SetStudentScheduleCommand>>()
            .WithName("CheckStudentSchedule")
            .Produces<ScheduleCheckResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        schedule.MapDelete("/", async (Guid memberId, ClaimsPrincipal user, EndStudentScheduleHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), memberId, ct)).ToHttpResult())
            .WithName("EndStudentSchedule")
            .Produces<StudentScheduleResponse>();

        var roster = app.MapGroup($"{SitePaths.Root}/timetable/days/{{date}}/students")
            .WithTags("Site bookings")
            .RequireAuthorization(Policies.SiteTrainer)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        roster.MapGet("/", async (DateOnly date, ClaimsPrincipal user, GetSessionStudentsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), date, ct)).ToHttpResult())
            .WithName("GetSessionStudents")
            .Produces<SessionStudentsResponse>();

        roster.MapPost("/remove", async (DateOnly date, ClaimsPrincipal user, RemoveSessionStudentCommand command, RemoveSessionStudentHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), date, command, ct)).ToHttpResult())
            .WithName("RemoveSessionStudent")
            .Produces<SessionStudentsResponse>();

        // Attendance from the desk's check-ins (BUSINESS_RULES.md §15 Attendance for the trainer, 15.7).
        var attendance = app.MapGroup($"{SitePaths.Root}/coaching")
            .WithTags("Site bookings")
            .RequireAuthorization(Policies.SiteTrainer)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // Today's, as 15.7 had it (the home's cards; an app still on the old page keeps working).
        attendance.MapGet("/today", async (ClaimsPrincipal user, GetTrainerDayHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), date: null, ct)).ToHttpResult())
            .WithName("GetTrainerToday")
            .Produces<TrainerDayResponse>();

        // Any day up to today (15.7d).
        attendance.MapGet("/days/{date}", async (DateOnly date, ClaimsPrincipal user, GetTrainerDayHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), date, ct)).ToHttpResult())
            .WithName("GetTrainerDay")
            .Produces<TrainerDayResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        attendance.MapGet("/students/{memberId:guid}/attendance", async (Guid memberId, DateOnly from, DateOnly to, ClaimsPrincipal user, GetStudentAttendanceHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), memberId, from, to, ct)).ToHttpResult())
            .WithName("GetStudentAttendance")
            .Produces<StudentAttendanceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        var mine = app.MapGroup($"{SitePaths.Root}/me/sessions")
            .WithTags("Site bookings")
            .RequireAuthorization(Policies.SiteMember)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mine.MapGet("/", async (ClaimsPrincipal user, GetMySessionsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetMySessions")
            .Produces<MySessionsResponse>();

        mine.MapPost("/leave", async (ClaimsPrincipal user, LeaveMySessionCommand command, LeaveMySessionHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), command, ct)).ToHttpResult())
            .WithName("LeaveMySession")
            .Produces<MySessionsResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        mine.MapPost("/return", async (ClaimsPrincipal user, ReturnToMySessionCommand command, ReturnToMySessionHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), command, ct)).ToHttpResult())
            .WithName("ReturnToMySession")
            .Produces<MySessionsResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        mine.MapPost("/move", async (ClaimsPrincipal user, MoveMySessionCommand command, MoveMySessionHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), command, ct)).ToHttpResult())
            .WithName("MoveMySession")
            .Produces<MySessionsResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // «سانس لغو شد» on the home (task 15.6c): the sessions the trainer cancelled, and «دیدم».
        mine.MapGet("/cancelled", async (ClaimsPrincipal user, GetMyCancellationsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetMyCancellations")
            .Produces<MyCancellationsResponse>();

        mine.MapPost("/cancelled/seen", async (ClaimsPrincipal user, SeeMyCancellationsCommand command, SeeMyCancellationsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), command, ct)).ToHttpResult())
            .WithName("SeeMyCancellations")
            .Produces<MyCancellationsResponse>();

        return app;
    }
}
