using Gym.Domain.Members;
using Gym.Infrastructure.Phones;

using Microsoft.Extensions.Options;

namespace Gym.Api.IntegrationTests.Members;

/// <summary>
/// Every way a receptionist might type the same Iranian mobile ends up as one E.164 string.
/// No database: libphonenumber is pure computation.
/// </summary>
public sealed class PhoneNormalizerTests
{
    private const string Expected = "+989121234567";

    private static readonly LibPhoneNumberNormalizer Normalizer = new(Options.Create(new PhoneOptions { PhoneDefaultRegion = "IR" }));

    /// <summary>Arabic-Indic digits look almost like Persian ones, so they are built from code points.</summary>
    private static string ArabicIndic(string englishDigits) =>
        new([.. englishDigits.Select(digit => char.IsAsciiDigit(digit) ? (char)(0x0660 + (digit - '0')) : digit)]);

    public static TheoryData<string> SameNumber => new()
    {
        "09121234567",
        "۰۹۱۲۱۲۳۴۵۶۷",
        ArabicIndic("09121234567"),
        "0912 123 4567",
        "0912-123-4567",
        "(0912) 123 4567",
        "9121234567",
        "+98 912 123 4567",
        "+989121234567",
        "0098 912 123 4567",
        "  09121234567  ",
    };

    [Theory]
    [MemberData(nameof(SameNumber))]
    public void Normalize_AnySpellingOfOneMobile_GivesOneE164String(string input)
    {
        Normalizer.Normalize(input).Value.ShouldBe(Expected);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0912")]
    [InlineData("091212345678901")]
    [InlineData("")]
    public void Normalize_NotAPhoneNumber_FailsWithPhoneInvalid(string input)
    {
        Normalizer.Normalize(input).Error.ShouldBe(MemberErrors.PhoneInvalid);
    }

    [Theory]
    [InlineData("02188776655")] // Tehran landline
    [InlineData("+98 21 8877 6655")]
    public void Normalize_IranianLandline_FailsWithPhoneNotMobile(string input)
    {
        Normalizer.Normalize(input).Error.ShouldBe(MemberErrors.PhoneNotMobile);
    }

    [Theory]
    [InlineData("+447911123456")] // United Kingdom mobile
    [InlineData("+971501234567")] // United Arab Emirates mobile
    public void Normalize_ForeignNumber_FailsWithPhoneNotIranian(string input)
    {
        Normalizer.Normalize(input).Error.ShouldBe(MemberErrors.PhoneNotIranian);
    }
}
