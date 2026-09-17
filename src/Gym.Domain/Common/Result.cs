namespace Gym.Domain.Common;

/// <summary>
/// The outcome of an operation that is allowed to fail in an expected way.
/// </summary>
/// <remarks>
/// <para>
/// Expected failures are return values here, not exceptions. A signature of
/// <c>Task&lt;Result&lt;MemberResponse&gt;&gt;</c> tells the caller that this can fail and
/// makes the compiler push the failure through the same path as the success; an exception
/// would say nothing in the signature and cost a stack unwind for something that is not
/// exceptional at all. Exceptions stay for the genuinely unexpected — a dropped connection,
/// a bug — which is what <c>GlobalExceptionHandler</c> in Gym.Api turns into a 500.
/// </para>
/// <para>
/// There is deliberately no "no error" sentinel value. A success simply holds no error, and
/// asking one for its <see cref="Error"/> throws, mirroring <see cref="Result{TValue}.Value"/>
/// throwing on a failure. Both directions of the mistake fail loudly instead of handing back
/// a blank <see cref="Common.Error"/> that would travel on and be reported as a real failure.
/// </para>
/// </remarks>
public class Result
{
    private readonly Error? _error;

    /// <summary>Creates a successful result. Use <see cref="Success()"/>.</summary>
    protected Result()
    {
    }

    /// <summary>Creates a failed result. Use <see cref="Failure(Error)"/>.</summary>
    protected Result(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        _error = error;
    }

    public bool IsSuccess => _error is null;

    public bool IsFailure => !IsSuccess;

    /// <summary>The failure this result carries.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error Error =>
        _error ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new();

    public static Result Failure(Error error) => new(error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value);

    public static Result<TValue> Failure<TValue>(Error error) => new(error);
}

/// <summary>
/// A <see cref="Result"/> that carries a value when it succeeds.
/// </summary>
/// <typeparam name="TValue">Usually a response record built by a handler.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue _value;

    internal Result(TValue value)
    {
        // A success carrying null is always a bug: the caller would read Value, get null, and
        // discover it several layers away from the handler that returned it.
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
    }

    internal Result(Error error)
        : base(error)
    {
        _value = default!;
    }

    /// <summary>The value produced by a successful operation.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public TValue Value =>
        IsSuccess ? _value : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>
    /// Lets a handler write <c>return response;</c> instead of <c>return Result.Success(response);</c>.
    /// The explicit form is <see cref="Result.Success{TValue}(TValue)"/>, which stays available.
    /// </summary>
    public static implicit operator Result<TValue>(TValue value) => new(value);
}
