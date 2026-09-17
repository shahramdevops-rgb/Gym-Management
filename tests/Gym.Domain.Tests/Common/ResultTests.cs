using Gym.Domain.Common;

namespace Gym.Domain.Tests.Common;

/// <summary>
/// The point of <see cref="Result"/> is that a failure cannot be mistaken for a success, in
/// either direction. These tests pin both halves of that promise, including the two ways a
/// caller can ask a result for something it does not have.
/// </summary>
public sealed class ResultTests
{
    private static readonly Error SampleError =
        Error.NotFound("Members.NotFound", "No member has that id.");

    [Fact]
    public void Success_WhenCreated_IsSuccess()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
    }

    [Fact]
    public void Failure_WhenCreated_IsFailure()
    {
        var result = Result.Failure(SampleError);

        result.IsFailure.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Failure_WhenCreated_ExposesTheError()
    {
        var result = Result.Failure(SampleError);

        result.Error.ShouldBe(SampleError);
    }

    [Fact]
    public void Error_WhenResultIsSuccess_Throws()
    {
        var result = Result.Success();

        // There is no Error.None sentinel on purpose. Returning a blank error here would let
        // it travel on and be reported to the caller as a real failure with an empty code.
        Should.Throw<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_WhenErrorIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void SuccessOfT_WhenCreated_ExposesTheValue()
    {
        var result = Result.Success("member-1");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("member-1");
    }

    [Fact]
    public void Value_WhenResultIsFailure_Throws()
    {
        var result = Result.Failure<string>(SampleError);

        // The mirror image of asking a success for its error: a failed result has no value,
        // and default(TValue) would be a null that surfaces far from here.
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void FailureOfT_WhenCreated_KeepsTheErrorType()
    {
        var result = Result.Failure<string>(SampleError);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void SuccessOfT_WhenValueIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Result.Success<string>(null!));
    }

    [Fact]
    public void ImplicitConversion_WhenValueAssigned_ProducesSuccess()
    {
        // This is what lets a handler end with `return response;`.
        Result<string> result = "member-1";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("member-1");
    }

    [Fact]
    public void ResultOfT_WhenFailed_IsAlsoAResult()
    {
        // Result<T> deriving from Result is what lets the API map both with one extension.
        Result result = Result.Failure<string>(SampleError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
    }
}
