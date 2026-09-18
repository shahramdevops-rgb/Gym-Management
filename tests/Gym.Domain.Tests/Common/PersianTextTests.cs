using Gym.Domain.Common.Text;

namespace Gym.Domain.Tests.Common;

/// <summary>
/// BUSINESS_RULES.md §13. Inputs are built from numeric code points, so the test shows exactly
/// which character goes in; a literal "ي" and "ی" are indistinguishable on screen.
/// </summary>
public sealed class PersianTextTests
{
    private static string Chars(params int[] codePoints) => new([.. codePoints.Select(code => (char)code)]);

    private const string Ali = "علی"; // ع ل ی (Persian ye, U+06CC)

    [Fact]
    public void Normalize_ArabicYe_BecomesPersianYe()
    {
        var arabicAli = Chars(0x0639, 0x0644, 0x064A); // ع ل ي

        PersianText.Normalize(arabicAli).ShouldBe(Ali);
    }

    [Fact]
    public void Normalize_AlefMaksura_BecomesPersianYe()
    {
        PersianText.Normalize(Chars(0x0639, 0x0644, 0x0649)).ShouldBe(Ali);
    }

    [Fact]
    public void Normalize_ArabicKaf_BecomesPersianKaf()
    {
        PersianText.Normalize(Chars(0x0643)).ShouldBe(Chars(0x06A9));
    }

    [Fact]
    public void Normalize_ZeroWidthNonJoiner_BecomesASpace()
    {
        var withHalfSpace = "میر" + Chars(0x200C) + "حسین";

        PersianText.Normalize(withHalfSpace).ShouldBe("میر حسین");
    }

    [Fact]
    public void Normalize_HarakatTatweelAndInvisibleMarks_AreRemoved()
    {
        // ع + fatha, tatweel, ل + kasra, ی, then an RTL mark and a zero-width space.
        var decorated = Chars(0x0639, 0x064E, 0x0640, 0x0644, 0x0650, 0x06CC, 0x200F, 0x200B);

        PersianText.Normalize(decorated).ShouldBe(Ali);
    }

    [Fact]
    public void Normalize_Whitespace_IsTrimmedAndCollapsed()
    {
        PersianText.Normalize("  رضا \t  احمدی  ").ShouldBe("رضا احمدی");
    }

    [Theory]
    [InlineData(new[] { 0x06F0, 0x06F9, 0x06F1, 0x06F2 }, "0912")] // Persian digits
    [InlineData(new[] { 0x0660, 0x0669, 0x0661, 0x0662 }, "0912")] // Arabic-Indic digits
    public void NormalizeDigits_PersianAndArabicDigits_BecomeEnglish(int[] codePoints, string expected)
    {
        PersianText.NormalizeDigits(Chars(codePoints)).ShouldBe(expected);
        PersianText.Normalize(Chars(codePoints)).ShouldBe(expected);
    }

    [Fact]
    public void NormalizeDigits_OtherCharacters_AreUntouched()
    {
        PersianText.NormalizeDigits("رمز" + Chars(0x06F1) + "a").ShouldBe("رمز1a");
    }
}
