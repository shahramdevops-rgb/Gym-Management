namespace Gym.Domain.Common;

/// <summary>
/// One expected failure, described in a way both the API and the Persian frontend can use.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Code"/> is the contract. It is stable, English, and shaped
/// <c>Feature.Reason</c> (for example <c>Members.PhoneAlreadyExists</c>); the frontend maps it
/// to a Persian sentence in <c>web/src/lib/errors.ts</c>. <see cref="Description"/> is for
/// developers and logs, so rewording it can never break a client — which is exactly why the
/// two are separate fields instead of one message.
/// </para>
/// <para>
/// A record, not a class: two errors with the same code and type are the same error, so tests
/// can assert <c>result.Error.ShouldBe(MemberErrors.NotFound)</c> without reference identity.
/// </para>
/// </remarks>
public sealed record Error
{
    public Error(string code, string description, ErrorType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Code = code;
        Description = description;
        Type = type;
    }

    /// <summary>The stable, machine-readable identity of this failure, e.g. <c>Members.NotFound</c>.</summary>
    public string Code { get; }

    /// <summary>A short English explanation for developers, logs and the API's ProblemDetails.</summary>
    public string Description { get; }

    /// <summary>The failure category, which Gym.Api turns into an HTTP status code.</summary>
    public ErrorType Type { get; }

    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    public static Error Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error BusinessRule(string code, string description) =>
        new(code, description, ErrorType.BusinessRule);
}
