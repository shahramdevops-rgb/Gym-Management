using Gym.Api.Common;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Gym.Api.Middleware;

/// <summary>
/// Last line of defence: turns anything that escaped a handler into a 500 ProblemDetails.
/// </summary>
/// <remarks>
/// <para>
/// Expected failures never reach here — they come back as a <c>Result</c> and are mapped by
/// <c>ResultExtensions</c>. What reaches here is a bug, a dropped connection, a disk full. The
/// response therefore says nothing about the cause: an exception message can carry a
/// connection string, a file path or a member's name, and it is useless to the person reading
/// it anyway. They get the correlation id instead, which finds the full exception in Seq.
/// </para>
/// <para>
/// Nothing is logged here on purpose. ASP.NET Core's exception handler middleware already
/// logs the exception at <c>Error</c> before calling this handler, and Serilog's request log
/// records the failed request; a third line would be the same event three times in Seq.
/// </para>
/// </remarks>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Writing through IProblemDetailsService rather than serialising here means the
        // customization in ProblemDetailsConfiguration runs for this response too, so a 500
        // carries the same correlationId field as every other error.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The request could not be completed. Quote the correlation id when reporting this.",
                Extensions = { [ProblemDetailsFields.Code] = ApiErrorCodes.Unexpected },
            },
        });
    }
}
