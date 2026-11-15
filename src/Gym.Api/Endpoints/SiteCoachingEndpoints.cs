using System.Security.Claims;

using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Bookings;
using Gym.Application.Coaching;
using Gym.Application.Coaching.AcceptTrainerRequest;
using Gym.Application.Coaching.CancelTrainerRequest;
using Gym.Application.Coaching.DeclineTrainerRequest;
using Gym.Application.Coaching.EndMyTrainer;
using Gym.Application.Coaching.EndStudent;
using Gym.Application.Coaching.GetMyCoachingNews;
using Gym.Application.Coaching.GetMyTrainer;
using Gym.Application.Coaching.GetStudentNews;
using Gym.Application.Coaching.GetStudents;
using Gym.Application.Coaching.ListTrainerChoices;
using Gym.Application.Coaching.RenewCoachingPeriod;
using Gym.Application.Coaching.RequestTrainer;
using Gym.Application.Coaching.SetCoachingDaysLeft;
using Gym.Application.Coaching.SeeMyCoachingNews;
using Gym.Application.Coaching.SeeStudentNews;

namespace Gym.Api.Endpoints;

/// <summary>
/// A member and their trainer on the site (BUSINESS_RULES.md §15 <i>A member and their trainer</i>):
/// the member's «مربی من» behind <see cref="Policies.SiteMember"/>, the trainer's requests and
/// «شاگردان من» behind <see cref="Policies.SiteTrainer"/>. The handlers read the roles again from the
/// database, since a token's may be 15 minutes old.
/// </summary>
public static class SiteCoachingEndpoints
{
    public static IEndpointRouteBuilder MapSiteCoachingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var root = $"{SitePaths.Root}/coaching";

        var member = app.MapGroup(root)
            .WithTags("Site coaching")
            .RequireAuthorization(Policies.SiteMember)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        member.MapGet("/trainers", async (ClaimsPrincipal user, ListTrainerChoicesHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("ListSiteTrainers")
            .Produces<IReadOnlyList<TrainerChoiceResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        member.MapGet("/my-trainer", async (ClaimsPrincipal user, GetMyTrainerHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetMyTrainer")
            .Produces<MyTrainerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        member.MapPost("/my-trainer/request", async (ClaimsPrincipal user, RequestTrainerCommand command, RequestTrainerHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), command, ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<RequestTrainerCommand>>()
            .WithName("RequestTrainer")
            .Produces<MyTrainerResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        member.MapPost("/my-trainer/cancel-request", async (ClaimsPrincipal user, CancelTrainerRequestHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("CancelTrainerRequest")
            .Produces<MyTrainerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        member.MapPost("/my-trainer/end", async (ClaimsPrincipal user, EndMyTrainerHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("EndMyTrainer")
            .Produces<MyTrainerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        member.MapGet("/my-trainer/news", async (ClaimsPrincipal user, GetMyCoachingNewsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetMyCoachingNews")
            .Produces<CoachingNewsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        member.MapPost("/my-trainer/news/seen", async (ClaimsPrincipal user, SeeCoachingNewsCommand command, SeeMyCoachingNewsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), command, ct)).ToHttpResult())
            .WithName("SeeMyCoachingNews")
            .Produces<CoachingNewsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var trainer = app.MapGroup(root)
            .WithTags("Site coaching")
            .RequireAuthorization(Policies.SiteTrainer)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        trainer.MapGet("/students", async (ClaimsPrincipal user, GetStudentsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetStudents")
            .Produces<StudentsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // The body is optional: an app still on the page before 15.7h sends none, and gets the 30 days.
        trainer.MapPost("/requests/{id:guid}/accept", async (
                Guid id, ClaimsPrincipal user, AcceptTrainerRequestCommand? command, AcceptTrainerRequestHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), id, command, ct)).ToHttpResult())
            .WithName("AcceptTrainerRequest")
            .Produces<StudentsResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        trainer.MapPost("/requests/{id:guid}/decline", async (Guid id, ClaimsPrincipal user, DeclineTrainerRequestHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), id, ct)).ToHttpResult())
            .WithName("DeclineTrainerRequest")
            .Produces<StudentsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        trainer.MapPost("/students/{memberId:guid}/end", async (Guid memberId, ClaimsPrincipal user, EndStudentHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), memberId, ct)).ToHttpResult())
            .WithName("EndStudent")
            .Produces<StudentsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        trainer.MapPost("/students/{memberId:guid}/period", async (
                Guid memberId, ClaimsPrincipal user, RenewCoachingPeriodCommand command, RenewCoachingPeriodHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), memberId, command, ct)).ToHttpResult())
            .WithName("RenewCoachingPeriod")
            .Produces<StudentScheduleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // «ویرایش دوره» (15.7k): the days left of a running period, earlier or later.
        trainer.MapPost("/students/{memberId:guid}/period/days-left", async (
                Guid memberId, ClaimsPrincipal user, SetCoachingDaysLeftCommand command, SetCoachingDaysLeftHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), memberId, command, ct)).ToHttpResult())
            .WithName("SetCoachingDaysLeft")
            .Produces<StudentScheduleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        trainer.MapGet("/students/news", async (ClaimsPrincipal user, GetStudentNewsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), ct)).ToHttpResult())
            .WithName("GetStudentNews")
            .Produces<CoachingNewsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        trainer.MapPost("/students/news/seen", async (ClaimsPrincipal user, SeeCoachingNewsCommand command, SeeStudentNewsHandler handler, CancellationToken ct) =>
                (await handler.Handle(SiteUser.AccountId(user), command, ct)).ToHttpResult())
            .WithName("SeeStudentNews")
            .Produces<CoachingNewsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
