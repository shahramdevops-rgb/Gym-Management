using Gym.Api.Common;
using Gym.Domain.Common;

using Microsoft.AspNetCore.Http;

namespace Gym.Api.IntegrationTests.Common;

/// <summary>
/// The one place that decides what a business failure looks like over HTTP. These tests read
/// the response the client would receive, not the result object the code returned.
/// </summary>
public sealed class ResultExtensionsTests
{
    public static TheoryData<ErrorType, int> ExpectedStatusCodes() => new()
    {
        { ErrorType.Validation, StatusCodes.Status400BadRequest },
        { ErrorType.Unauthorized, StatusCodes.Status401Unauthorized },
        { ErrorType.Forbidden, StatusCodes.Status403Forbidden },
        { ErrorType.NotFound, StatusCodes.Status404NotFound },
        { ErrorType.Conflict, StatusCodes.Status409Conflict },
        { ErrorType.BusinessRule, StatusCodes.Status422UnprocessableEntity },
    };

    [Theory]
    [MemberData(nameof(ExpectedStatusCodes))]
    public async Task ToHttpResult_WhenFailed_UsesTheStatusCodeForTheErrorType(ErrorType type, int expected)
    {
        var error = new Error("Members.Sample", "A sample failure.", type);

        var response = await ExecutedResult.RunAsync(Result.Failure(error).ToHttpResult());

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public void ToStatusCode_ForEveryErrorType_IsMapped()
    {
        // The compiler cannot enforce this: a C# enum can hold any underlying value, so the
        // switch needs a default arm and adding a member would compile. This test is what
        // turns "somebody added an ErrorType and forgot the status code" into a red build.
        foreach (var type in Enum.GetValues<ErrorType>())
        {
            Should.NotThrow(() => ResultExtensions.ToStatusCode(type));
            Should.NotThrow(() => ResultExtensions.ToTitle(type));
        }
    }

    [Fact]
    public async Task ToHttpResult_WhenFailed_WritesProblemDetailsWithTheErrorCode()
    {
        var error = Error.Conflict("Members.PhoneAlreadyExists", "Another member already uses that phone number.");

        var response = await ExecutedResult.RunAsync(Result.Failure(error).ToHttpResult());
        var body = response.Json();

        response.ContentType.ShouldStartWith("application/problem+json");
        response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);

        // The code is the contract web/src/lib/errors.ts maps to a Persian message. The
        // description is English on purpose: it is for developers and logs.
        body.GetProperty(ProblemDetailsFields.Code).GetString().ShouldBe("Members.PhoneAlreadyExists");
        body.GetProperty("detail").GetString().ShouldBe("Another member already uses that phone number.");
        body.GetProperty("title").GetString().ShouldBe("Conflict with the current state.");
        body.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ToHttpResult_WhenFailed_IncludesTheCorrelationId()
    {
        var response = await ExecutedResult.RunAsync(
            Result.Failure(Error.NotFound("Members.NotFound", "No member has that id.")).ToHttpResult());

        // Same id as the X-Correlation-Id header, in the body, because that is the one a user
        // can read off a screen and quote when reporting the failure.
        response.Json()
            .GetProperty(ProblemDetailsFields.CorrelationId)
            .GetString()
            .ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ToHttpResult_WhenSucceededWithoutValue_ReturnsNoContent()
    {
        var response = await ExecutedResult.RunAsync(Result.Success().ToHttpResult());

        response.StatusCode.ShouldBe(StatusCodes.Status204NoContent);
        response.Body.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToHttpResult_WhenSucceededWithValue_ReturnsOkAndTheValue()
    {
        var response = await ExecutedResult.RunAsync(Result.Success(new SampleResponse("member-1")).ToHttpResult());

        response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        // camelCase, because that is what the generated TypeScript client expects.
        response.Json().GetProperty("fullName").GetString().ShouldBe("member-1");
    }

    [Fact]
    public async Task ToHttpResult_WhenSucceededWithACustomShape_UsesIt()
    {
        var result = Result.Success(new SampleResponse("member-1"));

        var response = await ExecutedResult.RunAsync(
            result.ToHttpResult(value => Results.Created($"/api/members/{value.FullName}", value)));

        response.StatusCode.ShouldBe(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task ToHttpResult_WhenFailedWithValue_StillWritesTheProblem()
    {
        var error = Error.BusinessRule("Subscriptions.Expired", "The subscription has expired.");

        var response = await ExecutedResult.RunAsync(Result.Failure<SampleResponse>(error).ToHttpResult());

        response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        response.Json().GetProperty(ProblemDetailsFields.Code).GetString().ShouldBe("Subscriptions.Expired");
    }

    [Fact]
    public void ToHttpResult_WhenResultIsNull_Throws()
    {
        Result result = null!;

        Should.Throw<ArgumentNullException>(() => result.ToHttpResult());
    }

    private sealed record SampleResponse(string FullName);
}
