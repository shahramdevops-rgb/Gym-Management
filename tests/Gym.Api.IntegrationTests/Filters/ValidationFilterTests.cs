using FluentValidation;

using Gym.Api.Common;
using Gym.Api.Filters;
using Gym.Api.IntegrationTests.Common;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Filters;

/// <summary>
/// Task 0.5's "done when": an invalid request comes back as a 400 ProblemDetails whose field
/// errors carry codes, so the Persian frontend can attach a message to the right input.
/// </summary>
public sealed class ValidationFilterTests
{
    /// <summary>Stands in for a real command; the filter never looks at the type itself.</summary>
    private sealed record SampleCommand(string FullName, string PhoneNumber);

    private sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(command => command.FullName)
                .NotEmpty().WithErrorCode("Members.FullNameRequired").WithMessage("Full name is required.");

            // Two rules on one field, so the grouping below is exercised by a realistic case.
            RuleFor(command => command.PhoneNumber)
                .NotEmpty().WithErrorCode("Members.PhoneRequired").WithMessage("Phone number is required.")
                .MinimumLength(11).WithErrorCode("Members.PhoneTooShort").WithMessage("Phone number is too short.");
        }
    }

    private sealed class NestedPropertyValidator : AbstractValidator<SampleCommand>
    {
        public NestedPropertyValidator() =>
            RuleFor(command => command.FullName)
                .NotEmpty()
                .WithErrorCode("Members.CityRequired")
                .OverridePropertyName("Address.City");
    }

    private static EndpointFilterInvocationContext ContextFor(params object[] arguments) =>
        arguments.Length switch
        {
            0 => EndpointFilterInvocationContext.Create(new DefaultHttpContext()),
            _ => EndpointFilterInvocationContext.Create(new DefaultHttpContext(), arguments[0]),
        };

    [Fact]
    public async Task InvokeAsync_WhenRequestIsInvalid_ReturnsBadRequestWithFieldCodes()
    {
        var filter = new ValidationFilter<SampleCommand>(new SampleCommandValidator());
        var context = ContextFor(new SampleCommand(string.Empty, "0912"));

        var returned = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok()));
        var response = await ExecutedResult.RunAsync(returned.ShouldBeAssignableTo<IResult>()!);
        var body = response.Json();

        response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        response.ContentType.ShouldStartWith("application/problem+json");
        body.GetProperty(ProblemDetailsFields.Code).GetString().ShouldBe(ApiErrorCodes.ValidationFailed);

        // The key is the JSON property name the client sent, not the C# one, because the form
        // uses it to decide which input to mark red.
        var fullNameErrors = body.GetProperty(ProblemDetailsFields.Errors).GetProperty("fullName");

        fullNameErrors[0].GetProperty("code").GetString().ShouldBe("Members.FullNameRequired");
        fullNameErrors[0].GetProperty("description").GetString().ShouldBe("Full name is required.");
    }

    [Fact]
    public async Task InvokeAsync_WhenFieldBreaksTwoRules_ReportsBoth()
    {
        var filter = new ValidationFilter<SampleCommand>(new SampleCommandValidator());
        // Empty fails both NotEmpty and MinimumLength: FluentValidation's default cascade mode
        // runs every rule in the chain rather than stopping at the first failure.
        var context = ContextFor(new SampleCommand("علی رضایی", string.Empty));

        var returned = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok()));
        var response = await ExecutedResult.RunAsync(returned.ShouldBeAssignableTo<IResult>()!);

        var phoneErrors = response.Json().GetProperty(ProblemDetailsFields.Errors).GetProperty("phoneNumber");
        var codes = phoneErrors.EnumerateArray().Select(error => error.GetProperty("code").GetString()).ToArray();

        // One round trip should tell the user everything that is wrong with the field, not the
        // first thing that is wrong with it.
        phoneErrors.GetArrayLength().ShouldBe(2);
        codes.ShouldBe(["Members.PhoneRequired", "Members.PhoneTooShort"], ignoreOrder: true);
    }

    [Fact]
    public async Task InvokeAsync_WhenPropertyIsNested_CamelCasesEverySegment()
    {
        var filter = new ValidationFilter<SampleCommand>(new NestedPropertyValidator());
        var context = ContextFor(new SampleCommand(string.Empty, "09123456789"));

        var returned = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok()));
        var response = await ExecutedResult.RunAsync(returned.ShouldBeAssignableTo<IResult>()!);

        response.Json()
            .GetProperty(ProblemDetailsFields.Errors)
            .TryGetProperty("address.city", out _)
            .ShouldBeTrue("nested paths must be camelCased segment by segment.");
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestIsInvalid_DoesNotCallTheEndpoint()
    {
        var filter = new ValidationFilter<SampleCommand>(new SampleCommandValidator());
        var context = ContextFor(new SampleCommand(string.Empty, string.Empty));
        var endpointWasCalled = false;

        await filter.InvokeAsync(context, _ =>
        {
            endpointWasCalled = true;

            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // The whole point of a filter: the handler never sees input it would have to defend
        // itself against, and nothing was written to the database on the way to a 400.
        endpointWasCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestIsValid_ReturnsTheEndpointResult()
    {
        var filter = new ValidationFilter<SampleCommand>(new SampleCommandValidator());
        var context = ContextFor(new SampleCommand("علی رضایی", "09123456789"));
        var expected = Results.NoContent();

        var returned = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(expected));

        // Passed through untouched: a filter that rewrapped successful results would quietly
        // change every endpoint's response shape.
        returned.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task Filter_WhenCreatedByTheContainer_ResolvesItsValidator()
    {
        // This is how AddEndpointFilter<ValidationFilter<T>>() builds the filter, so a
        // validator registered by AddApplication's assembly scan reaches it the same way.
        var services = new ServiceCollection();
        services.AddScoped<IValidator<SampleCommand>, SampleCommandValidator>();

        await using var provider = services.BuildServiceProvider();

        var filter = ActivatorUtilities.CreateInstance<ValidationFilter<SampleCommand>>(provider);
        var context = ContextFor(new SampleCommand(string.Empty, string.Empty));

        var returned = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok()));
        var response = await ExecutedResult.RunAsync(returned.ShouldBeAssignableTo<IResult>()!);

        response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoArgumentOfTheExpectedType_Throws()
    {
        var filter = new ValidationFilter<SampleCommand>(new SampleCommandValidator());
        var context = ContextFor("not a command");

        // A wiring mistake must be loud. Skipping validation here would leave the endpoint
        // unprotected while every test and every request still looked perfectly healthy.
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok())));
    }
}
