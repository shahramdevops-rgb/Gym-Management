namespace Gym.Api.Common;

/// <summary>
/// The extension field names this API adds to every ProblemDetails body.
/// </summary>
/// <remarks>
/// These strings are a published contract: <c>web/src/lib/errors.ts</c> reads them to decide
/// which Persian message to show. They are named once here so that a rename is a compiler
/// change on this side and a deliberate, visible change on the other.
/// </remarks>
public static class ProblemDetailsFields
{
    /// <summary>The error code of the failure as a whole, e.g. <c>Members.PhoneAlreadyExists</c>.</summary>
    public const string Code = "code";

    /// <summary>Per-field validation failures, keyed by the JSON property name.</summary>
    public const string Errors = "errors";

    /// <summary>The request's correlation id, also returned in <c>X-Correlation-Id</c>.</summary>
    public const string CorrelationId = "correlationId";
}
