using Gym.Domain.Notifications;

namespace Gym.Domain.Tests.Notifications;

/// <summary>
/// How many parts a Persian SMS takes (BUSINESS_RULES.md §10 <i>Sending</i>). The frontend's
/// <c>smsParts</c> has the same cases.
/// </summary>
public sealed class SmsPartsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(70, 1)]
    [InlineData(71, 2)]
    [InlineData(134, 2)]
    [InlineData(135, 3)]
    [InlineData(201, 3)]
    [InlineData(202, 4)]
    public void Count_Length_IsTheNumberOfParts(int length, int expected)
    {
        SmsParts.Count(new string('ب', length)).ShouldBe(expected);
    }

    [Fact]
    public void Count_LineBreak_IsOneCharacter()
    {
        SmsParts.Count(new string('ب', 69) + "\n").ShouldBe(1);
    }

    [Fact]
    public void Count_Emoji_TakesTwoCharacters()
    {
        // 69 letters and one emoji are 71 UTF-16 code units, as UCS-2 sends them.
        SmsParts.Count(new string('ب', 69) + "😀").ShouldBe(2);
    }
}
