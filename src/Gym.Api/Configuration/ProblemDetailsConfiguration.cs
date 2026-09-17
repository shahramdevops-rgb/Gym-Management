using System.Diagnostics;

using Gym.Api.Common;

namespace Gym.Api.Configuration;

/// <summary>
/// Makes every error response in this API the same shape: RFC 9457 ProblemDetails carrying the
/// correlation id the caller also sees in the <c>X-Correlation-Id</c> header.
/// </summary>
/// <remarks>
/// One shape for every failure means the frontend needs exactly one error parser, whether the
/// 400 came from a validation filter, the 404 from a handler, or the 500 from a bug. The
/// correlation id is repeated in the body because that is what a user can copy out of a screen
/// and quote to the office — a response header is invisible to them.
/// </remarks>
public static class ProblemDetailsConfiguration
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // The same id CorrelationIdMiddleware returns, taken from the same place, so the
            // log line, the response header and the response body all agree.
            context.ProblemDetails.Extensions[ProblemDetailsFields.CorrelationId] =
                Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

            context.ProblemDetails.Instance ??=
                $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        });

        return services;
    }
}
