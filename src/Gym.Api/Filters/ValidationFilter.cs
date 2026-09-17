using System.Text.Json;

using FluentValidation;
using FluentValidation.Results;

using Gym.Api.Common;

namespace Gym.Api.Filters;

/// <summary>
/// Runs the FluentValidation validator for <typeparamref name="TRequest"/> before the endpoint,
/// and turns any failures into a 400 ProblemDetails with one entry per field.
/// </summary>
/// <typeparam name="TRequest">The command or query the endpoint binds from the request body.</typeparam>
/// <remarks>
/// <para>
/// Why a filter and not a line in every handler: shape checks ("phone is required", "page size
/// is at most 100") are the same work for every use case, and a handler that starts with
/// fifteen guard clauses buries the business rule it exists to express. The filter short
/// circuits before the handler runs, so handlers can assume well-formed input and are left
/// with the rules that actually need the database.
/// </para>
/// <para>
/// Why it lives in Gym.Api and not in Gym.Application, where docs/ARCHITECTURE.md first put it:
/// <c>IEndpointFilter</c> is an ASP.NET Core type, and Application must stay usable without a
/// web host (a layer dependency test fails the build if it references ASP.NET Core). The
/// validators themselves are Application's, because a command must be valid whoever sends it;
/// only the HTTP shape of the answer is the API's.
/// </para>
/// </remarks>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
    where TRequest : notnull
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Arguments.OfType<TRequest>().FirstOrDefault() is not { } request)
        {
            // A wiring mistake, not a bad request: the filter was attached to an endpoint that
            // takes no argument of this type. Failing loudly beats skipping validation
            // silently, which would leave the endpoint unprotected and look like it worked.
            throw new InvalidOperationException(
                $"The endpoint has no argument of type {typeof(TRequest).Name}, so {nameof(ValidationFilter<TRequest>)} cannot validate it.");
        }

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (validation.IsValid)
        {
            return await next(context);
        }

        return ToProblem(validation.Errors);
    }

    private static IResult ToProblem(IEnumerable<ValidationFailure> failures)
    {
        // Grouped by field, because one field can break several rules at once and the form
        // should be able to show them all rather than one per round trip.
        var errors = failures
            .GroupBy(failure => ToJsonPropertyName(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(failure => new ValidationErrorDetail(failure.ErrorCode, failure.ErrorMessage))
                    .ToArray(),
                StringComparer.Ordinal);

        return Results.Problem(
            detail: "One or more fields are invalid.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation failed.",
            extensions: new Dictionary<string, object?>
            {
                [ProblemDetailsFields.Code] = ApiErrorCodes.ValidationFailed,
                [ProblemDetailsFields.Errors] = errors,
            });
    }

    /// <summary>
    /// FluentValidation reports the C# property name (<c>PhoneNumber</c>); the client sent and
    /// expects the JSON one (<c>phoneNumber</c>), and it uses these keys to attach messages to
    /// its own form fields. Split on '.' so nested paths such as <c>Address.City</c> convert
    /// segment by segment.
    /// </summary>
    private static string ToJsonPropertyName(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
