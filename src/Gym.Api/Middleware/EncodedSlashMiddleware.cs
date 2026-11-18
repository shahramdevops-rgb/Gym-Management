using Gym.Api.Common;

using Microsoft.AspNetCore.Http.Features;

namespace Gym.Api.Middleware;

/// <summary>
/// Refuses (400) a request whose path carries an encoded slash (<c>%2F</c>) or backslash
/// (<c>%5C</c>), on every host, before anything routes it (found in 16.4's review).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> Caddy decides which host may reach which part of the API by the path (the gym's
/// public address reaches <c>/api/site/shop</c> only, deploy/Caddyfile). It matches the path
/// decoded and cleaned of <c>..</c>, then forwards it as it was sent. Kestrel does not decode
/// <c>%2F</c> before it removes the <c>..</c> segments, so the two can disagree on where a path
/// ends: <c>/api/site/shop/a%2Fb%2Fc/../../../auth/login</c> is the shop's path to Caddy and the
/// staff login to the API. Without an encoded slash they always agree.
/// </para>
/// <para>
/// <b>Nothing is lost.</b> No endpoint takes a slash inside one segment: ids are GUIDs, sizes are
/// words, and a search or a date travels in the query string, which is not looked at here.
/// </para>
/// </remarks>
public sealed class EncodedSlashMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!HasEncodedSlash(context))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problemDetails = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid path.",
                Detail = "The path may not contain an encoded slash or backslash.",
                Extensions = { [ProblemDetailsFields.Code] = ApiErrorCodes.InvalidPath },
            },
        });
    }

    /// <summary>
    /// The path as the client sent it (<c>RawTarget</c>, without its query), or the routed path
    /// when the server keeps no raw target; Kestrel leaves <c>%2F</c> encoded in the latter too.
    /// </summary>
    public static bool HasEncodedSlash(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        var path = string.IsNullOrEmpty(raw) ? context.Request.Path.Value ?? string.Empty : raw.Split('?', 2)[0];

        return path.Contains("%2f", StringComparison.OrdinalIgnoreCase)
            || path.Contains("%5c", StringComparison.OrdinalIgnoreCase);
    }
}
