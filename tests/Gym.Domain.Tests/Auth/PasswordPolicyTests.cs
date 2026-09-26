using Gym.Domain.Auth;

namespace Gym.Domain.Tests.Auth;

public sealed class PasswordPolicyTests
{
    private const string GoodPassword = "tabriz lamp 4 kettle";

    [Theory]
    [InlineData(GoodPassword)]
    [InlineData("TabrizLamp4Kettle")]
    [InlineData("kettle-under-7-lamps")]
    [InlineData("mX9#vQ2!rT7p")] // exactly the minimum length
    public void Check_LongUncommonPassword_Succeeds(string password)
    {
        PasswordPolicy.Check(password, "reza").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Check_NoUpperCaseDigitOrSymbol_Succeeds()
    {
        // NIST: no composition rules. Length is what counts.
        PasswordPolicy.Check("lamps and kettles", userName: null).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("mX9#vQ2!rT7")] // one short of the minimum
    public void Check_ShorterThanMinimum_FailsWithTooShort(string password)
    {
        PasswordPolicy.Check(password, userName: null).Error.ShouldBe(PasswordErrors.TooShort);
    }

    [Fact]
    public void Check_NullPassword_FailsWithTooShort()
    {
        PasswordPolicy.Check(password: null, userName: null).Error.ShouldBe(PasswordErrors.TooShort);
    }

    [Fact]
    public void Check_LongerThanMaximum_FailsWithTooLong()
    {
        var password = string.Concat(Enumerable.Repeat("kettle lamp ", 11)); // 132 characters

        PasswordPolicy.Check(password, userName: null).Error.ShouldBe(PasswordErrors.TooLong);
    }

    [Theory]
    [InlineData("رمز عبور خیلی خوب من")] // Persian letters
    [InlineData("tabriz lamp ۴ kettle")] // a Persian digit the caller forgot to convert
    [InlineData("tabriz lamp é kettle")] // Latin, but not English
    [InlineData("tabriz\tlamp 4 kettle")] // a tab is not a printable character
    [InlineData("رمز")] // short and Persian: the Persian is the thing to fix first
    public void Check_NonEnglishCharacter_FailsWithNotEnglish(string password)
    {
        PasswordPolicy.Check(password, userName: null).Error.ShouldBe(PasswordErrors.NotEnglish);
    }

    [Theory]
    [InlineData("reza lamp 4 kettle", "reza")]
    [InlineData("REZA lamp 4 kettle", "reza")] // case does not hide it
    [InlineData("lamp4kettle.Owner", "Owner")]
    public void Check_ContainsUserName_FailsWithContainsUserName(string password, string userName)
    {
        PasswordPolicy.Check(password, userName).Error.ShouldBe(PasswordErrors.ContainsUserName);
    }

    [Theory]
    [InlineData("aaaaaaaaaaaa")]
    [InlineData("abababababab")]
    [InlineData("1212121212121212")]
    [InlineData("abcdabcdabcd")] // four different characters
    [InlineData("abcdefghijklm")]
    [InlineData("123456789012")] // the digits wrap from 9 to 0
    [InlineData("210987654321")] // backwards
    [InlineData("123456789012345678901234567890")] // a digit run longer than one lap
    [InlineData("QWERTYUIOPASD")]
    [InlineData("1qaz2wsx3edc4rfv")]
    public void Check_RepetitionOrSequence_FailsWithTooSimple(string password)
    {
        PasswordPolicy.Check(password, userName: null).Error.ShouldBe(PasswordErrors.TooSimple);
    }

    [Theory]
    [InlineData("password1234")] // a common word with digits after it
    [InlineData("Football2024!")] // case, a year and a symbol do not disguise it
    [InlineData("!!!sunshine2024")] // symbols in front too
    [InlineData("iloveyou 12345")]
    [InlineData("gymplus2026!")] // an ordinary gym word as a whole password
    public void Check_CommonPassword_FailsWithTooCommon(string password)
    {
        PasswordPolicy.Check(password, userName: null).Error.ShouldBe(PasswordErrors.TooCommon);
    }

    [Theory]
    [InlineData("pasargadplas")] // reported by the developer: the gym's name with a typo in "plus"
    [InlineData("Pasargad@1405")]
    [InlineData("Pasargad Gym Plus 1")]
    [InlineData("bashgah123456")]
    [InlineData("my-pasargad-locker-key")] // inside a longer password too
    [InlineData("P@sarg4d-is-mine-1")] // look-alike substitutions
    [InlineData("PAS.AR.GAD.2026")] // separators
    [InlineData("i love varzesh a lot")]
    [InlineData("badansazi-club-1405")]
    public void Check_ContainsTheGymName_FailsWithContainsGymName(string password)
    {
        PasswordPolicy.Check(password, userName: null).Error.ShouldBe(PasswordErrors.ContainsGymName);
    }

    [Theory]
    [InlineData("i train at the gym every morning")] // "gym" alone is too ordinary to refuse in a passphrase
    [InlineData("my fitness plan is long")]
    public void Check_OrdinaryGymWordInsideAPassphrase_Succeeds(string password)
    {
        PasswordPolicy.Check(password, userName: null).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Check_CommonWordInsideALongerPassphrase_Succeeds()
    {
        // Equality with the list, not "contains": a passphrase is not weak for having a word in it.
        PasswordPolicy.Check("my football is in the car", userName: null).IsSuccess.ShouldBeTrue();
    }
}
