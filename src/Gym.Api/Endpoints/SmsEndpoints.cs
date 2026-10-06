using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Notifications;
using Gym.Application.Notifications.GetSmsSettings;
using Gym.Application.Notifications.UpdateSmsSettings;

namespace Gym.Api.Endpoints;

/// <summary>
/// The SMS pages (BUSINESS_RULES.md §10). Owner only (§1, permissions): every SMS costs money, and
/// the Owner is the one who pays.
/// </summary>
/// <remarks>
/// Each endpoint names its own policy rather than inheriting one from the group, so a new endpoint
/// here cannot quietly get the wrong one (the lesson of task 6.5.1). The history and resend join
/// this group in task 10.5.
/// </remarks>
public static class SmsEndpoints
{
    public static IEndpointRouteBuilder MapSmsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var sms = app.MapGroup("/api/sms")
            .WithTags("Sms")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        sms.MapGet("/settings", async (GetSmsSettingsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("GetSmsSettings")
            .Produces<SmsSettingsResponse>();

        sms.MapPut("/settings", async (UpdateSmsSettingsCommand command, UpdateSmsSettingsHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly)
            .AddEndpointFilter<ValidationFilter<UpdateSmsSettingsCommand>>()
            .WithName("UpdateSmsSettings")
            .Produces<SmsSettingsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
