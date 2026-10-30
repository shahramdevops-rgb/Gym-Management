using System.Security.Claims;

using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Timetables;
using Gym.Application.Timetables.GetTimetable;
using Gym.Application.Timetables.ResetTimetableDay;
using Gym.Application.Timetables.SetTimetableDay;
using Gym.Application.Timetables.SetWeekPattern;

namespace Gym.Api.Endpoints;

/// <summary>
/// The trainer's «برنامه سانس‌ها» on the site (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>,
/// task 15.6a), behind <see cref="Policies.SiteTrainer"/>. No path names a trainer: it is always the
/// signed-in account's own timetable. The handlers read the trainer again from the database, since
/// a token's roles may be 15 minutes old.
/// </summary>
public static class SiteTimetableEndpoints
{
    public static IEndpointRouteBuilder MapSiteTimetableEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup($"{SitePaths.Root}/timetable")
            .WithTags("Site timetable")
            .RequireAuthorization(Policies.SiteTrainer)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", async (ClaimsPrincipal user, GetTimetableHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetTimetable")
            .Produces<TimetableResponse>();

        group.MapPut("/pattern", async (ClaimsPrincipal user, SetWeekPatternCommand command, SetWeekPatternHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<SetWeekPatternCommand>>()
            .WithName("SetWeekPattern")
            .Produces<TimetableResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/days/{date}", async (DateOnly date, ClaimsPrincipal user, SetTimetableDayCommand command, SetTimetableDayHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), date, command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<SetTimetableDayCommand>>()
            .WithName("SetTimetableDay")
            .Produces<TimetableResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/days/{date}", async (DateOnly date, ClaimsPrincipal user, ResetTimetableDayHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), date, ct)).ToHttpResult())
            .WithName("ResetTimetableDay")
            .Produces<TimetableResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
