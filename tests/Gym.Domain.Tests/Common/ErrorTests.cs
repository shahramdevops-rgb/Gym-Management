using Gym.Domain.Common;

namespace Gym.Domain.Tests.Common;

/// <summary>
/// An error's code is a published contract, so the parts that make it up are guarded here:
/// the category each factory stamps, and the value equality tests rely on.
/// </summary>
public sealed class ErrorTests
{
    public static TheoryData<Func<string, string, Error>, ErrorType> Factories() => new()
    {
        { Error.Validation, ErrorType.Validation },
        { Error.Unauthorized, ErrorType.Unauthorized },
        { Error.Forbidden, ErrorType.Forbidden },
        { Error.NotFound, ErrorType.NotFound },
        { Error.Conflict, ErrorType.Conflict },
        { Error.BusinessRule, ErrorType.BusinessRule },
    };

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factory_WhenCalled_SetsMatchingType(Func<string, string, Error> factory, ErrorType expected)
    {
        var error = factory("Members.Sample", "A sample failure.");

        error.Type.ShouldBe(expected);
        error.Code.ShouldBe("Members.Sample");
        error.Description.ShouldBe("A sample failure.");
    }

    [Fact]
    public void Equality_WhenSameCodeAndType_AreEqual()
    {
        var first = Error.Conflict("Members.PhoneAlreadyExists", "That phone number is taken.");
        var second = Error.Conflict("Members.PhoneAlreadyExists", "That phone number is taken.");

        // Value equality is why a test can assert `result.Error.ShouldBe(MemberErrors.Duplicate)`
        // against a freshly built error instead of the one instance the handler happened to use.
        first.ShouldBe(second);
    }

    [Fact]
    public void Equality_WhenCodesDiffer_AreNotEqual()
    {
        var first = Error.Conflict("Members.PhoneAlreadyExists", "That phone number is taken.");
        var second = Error.Conflict("Members.NationalIdAlreadyExists", "That phone number is taken.");

        first.ShouldNotBe(second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenCodeIsBlank_Throws(string? code)
    {
        // A blank code would reach the frontend, match nothing in lib/errors.ts, and show the
        // member a message about an error that has no name.
        Should.Throw<ArgumentException>(() => Error.Validation(code!, "A sample failure."));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenDescriptionIsBlank_Throws(string? description)
    {
        Should.Throw<ArgumentException>(() => Error.Validation("Members.Sample", description!));
    }
}
