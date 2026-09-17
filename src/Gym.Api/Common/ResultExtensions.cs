using Gym.Domain.Common;

namespace Gym.Api.Common;

/// <summary>
/// The single place where a domain <see cref="Result"/> becomes an HTTP response.
/// </summary>
/// <remarks>
/// <para>
/// The mapping table lives here and nowhere else. If each endpoint chose its own status code,
/// "member not found" would be a 404 in one place and a 400 in another, and the frontend would
/// need a special case per endpoint. Endpoints therefore read
/// <c>(await handler.Handle(command, ct)).ToHttpResult()</c> and contain no status codes at all.
/// </para>
/// <para>
/// The failure body is a ProblemDetails carrying the error's <see cref="Error.Code"/> in the
/// <c>code</c> field. The code is what <c>web/src/lib/errors.ts</c> maps to a Persian message;
/// <c>detail</c> is the English description, which is for developers and logs.
/// </para>
/// </remarks>
public static class ResultExtensions
{
    /// <summary>Success becomes <c>204 No Content</c>: the operation worked and has nothing to return.</summary>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
    }

    /// <summary>Success becomes <c>200 OK</c> with the value as the body.</summary>
    public static IResult ToHttpResult<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>
    /// Success is shaped by <paramref name="onSuccess"/>, for the cases where 200 is the wrong
    /// answer — typically <c>Results.Created</c> after an insert.
    /// </summary>
    public static IResult ToHttpResult<TValue>(this Result<TValue> result, Func<TValue, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value) : Problem(result.Error);
    }

    private static IResult Problem(Error error) => Results.Problem(
        detail: error.Description,
        statusCode: ToStatusCode(error.Type),
        title: ToTitle(error.Type),
        extensions: new Dictionary<string, object?> { [ProblemDetailsFields.Code] = error.Code });

    /// <summary>
    /// The table from docs/ARCHITECTURE.md.
    /// </summary>
    /// <remarks>
    /// The default arm exists only because the compiler demands it: a C# enum can hold any
    /// value of its underlying type, so even a switch covering every declared member is not
    /// exhaustive (CS8524). It throws rather than guessing a status code, and
    /// <c>ResultExtensionsTests</c> walks <c>Enum.GetValues</c> so that adding a member to
    /// <see cref="ErrorType"/> without deciding its status code fails a test immediately.
    /// </remarks>
    internal static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,

        // 422, not 400: the request is syntactically fine and the caller is allowed to make
        // it; a rule in docs/BUSINESS_RULES.md is what says no. The distinction matters to the
        // frontend, which can re-render a form for a 400 but must show a message for a 422.
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No status code is mapped for this error type."),
    };

    internal static string ToTitle(ErrorType type) => type switch
    {
        ErrorType.Validation => "Invalid request.",
        ErrorType.Unauthorized => "Authentication required.",
        ErrorType.Forbidden => "Access denied.",
        ErrorType.NotFound => "Resource not found.",
        ErrorType.Conflict => "Conflict with the current state.",
        ErrorType.BusinessRule => "Business rule violated.",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No title is mapped for this error type."),
    };
}
