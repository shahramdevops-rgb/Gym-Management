using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Application.Common.Paging;
using Gym.Application.Notifications;
using Gym.Application.Notifications.Broadcasts;
using Gym.Application.Notifications.Broadcasts.GetBroadcastAudience;
using Gym.Application.Notifications.Broadcasts.ListBroadcasts;
using Gym.Application.Notifications.Broadcasts.PreviewBroadcast;
using Gym.Application.Notifications.Broadcasts.ResumeBroadcast;
using Gym.Application.Notifications.Broadcasts.SendBroadcast;
using Gym.Application.Notifications.GetSignInCodeSms;
using Gym.Application.Notifications.GetSmsCredit;
using Gym.Application.Notifications.GetSmsSamples;
using Gym.Application.Notifications.GetSmsSettings;
using Gym.Application.Notifications.ListSmsMessages;
using Gym.Application.Notifications.ResendSms;
using Gym.Application.Notifications.UpdateSmsSettings;

namespace Gym.Api.Endpoints;

/// <summary>
/// The SMS pages (BUSINESS_RULES.md §10). Owner only (§1, permissions): every SMS costs money, and
/// the Owner is the one who pays.
/// </summary>
/// <remarks>
/// Each endpoint names its own policy rather than inheriting one from the group, so a new endpoint
/// here cannot quietly get the wrong one (the lesson of task 6.5.1).
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

        // Each kind's text with a sample member, built by the code that writes the real one (§10).
        sms.MapGet("/samples", (GetSmsSamplesHandler handler) => Results.Ok(handler.Handle()))
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("GetSmsSamples")
            .Produces<SmsSamplesResponse>();

        // Always 200: a provider that does not answer is a null credit, not an error (§10).
        sms.MapGet("/credit", async (GetSmsCreditHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("GetSmsCredit")
            .Produces<SmsCreditResponse>();

        // The site's sign-in codes: counts and cost only, never a code (BUSINESS_RULES.md §15).
        sms.MapGet("/sign-in-codes", async (GetSignInCodeSmsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("GetSignInCodeSms")
            .Produces<SignInCodeSmsResponse>();

        sms.MapGet("/messages", async (
                [AsParameters] ListSmsMessagesQuery query,
                ListSmsMessagesHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .RequireAuthorization(Policies.OwnerOnly)
            .AddEndpointFilter<ValidationFilter<ListSmsMessagesQuery>>()
            .WithName("ListSmsMessages")
            .Produces<SmsMessageListResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // No body: a resend sends exactly the message that was written (§10).
        sms.MapPost("/messages/{id:guid}/resend", async (Guid id, ResendSmsHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("ResendSms")
            .Produces<SmsMessageResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // The broadcast (task 10.13): the groups' sizes, the confirmation's figures, the send, «ادامه ارسال», the list.
        sms.MapGet("/broadcasts/audiences", async (GetBroadcastAudienceHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("GetBroadcastAudiences")
            .Produces<BroadcastAudiencesResponse>();

        // A POST although it writes nothing: the text can be long and has line breaks.
        sms.MapPost("/broadcasts/preview", async (PreviewBroadcastCommand command, PreviewBroadcastHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("PreviewBroadcast")
            .Produces<BroadcastPreviewResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        sms.MapPost("/broadcasts", async (SendBroadcastCommand command, SendBroadcastHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("SendBroadcast")
            .Produces<BroadcastResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        sms.MapPost("/broadcasts/{id:guid}/resume", async (Guid id, ResumeBroadcastHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly)
            .WithName("ResumeBroadcast")
            .Produces<BroadcastResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        sms.MapGet("/broadcasts", async (
                [AsParameters] ListBroadcastsQuery query,
                ListBroadcastsHandler handler,
                CancellationToken ct) =>
                    Results.Ok(await handler.Handle(query, ct)))
            .RequireAuthorization(Policies.OwnerOnly)
            .AddEndpointFilter<ValidationFilter<ListBroadcastsQuery>>()
            .WithName("ListBroadcasts")
            .Produces<PagedResponse<BroadcastResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
