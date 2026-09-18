using Gym.Infrastructure.Identity;

namespace Gym.Api.IntegrationTests.Identity;

/// <summary>
/// No database, no DI container: <see cref="LetterAndDigitPasswordValidator"/> only inspects
/// the password string, so <c>manager</c> and <c>user</c> are irrelevant to the outcome and
/// passed as <c>null!</c> rather than built for real.
/// </summary>
public sealed class LetterAndDigitPasswordValidatorTests
{
    [Theory]
    [InlineData("onlyletters", false)]
    [InlineData("12345678", false)]
    [InlineData("!@#$%^&*", false)] // symbols alone satisfy neither check
    [InlineData("password1", true)]
    [InlineData("PASSWORD1", true)] // no case requirement
    public async Task ValidateAsync_WhenChecked_RequiresALetterAndADigitRegardlessOfCase(
        string password,
        bool expectedValid)
    {
        var validator = new LetterAndDigitPasswordValidator();

        var result = await validator.ValidateAsync(manager: null!, user: null!, password);

        result.Succeeded.ShouldBe(expectedValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenPasswordIsNull_Fails()
    {
        var validator = new LetterAndDigitPasswordValidator();

        var result = await validator.ValidateAsync(manager: null!, user: null!, password: null);

        result.Succeeded.ShouldBeFalse();
    }
}
