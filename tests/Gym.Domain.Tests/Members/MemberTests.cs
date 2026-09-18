using Gym.Domain.Members;

namespace Gym.Domain.Tests.Members;

public sealed class MemberTests
{
    private const string Phone = "+989121234567";

    // Look-alike and invisible characters as code points: on screen they match their Persian twins.
    private const char ArabicYe = (char)0x064A;
    private const char ArabicKaf = (char)0x0643;
    private const char HalfSpace = (char)0x200C;

    [Fact]
    public void Create_ValidDetails_IsActiveWithTheNormalizedName()
    {
        // "Ali" typed with the Arabic ye, then a half-space before the family name.
        var typed = $"  عل{ArabicYe} {HalfSpace}رضایی ";

        var member = Member.Create(typed, Phone, notes: null).Value;

        member.IsActive.ShouldBeTrue();
        member.FullName.ShouldBe(typed.Trim(), "the display name keeps what was typed, trimmed.");
        member.NormalizedFullName.ShouldBe("علی رضایی");
        member.NormalizedFullName.ShouldNotContain(ArabicYe);
        member.PhoneNumber.ShouldBe(Phone);
    }

    [Fact]
    public void Create_LatinName_NormalizedColumnIsLowerCase()
    {
        Member.Create("Sara Smith", Phone, null).Value.NormalizedFullName.ShouldBe("sara smith");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_FailsWithFullNameRequired(string fullName)
    {
        Member.Create(fullName, Phone, null).Error.ShouldBe(MemberErrors.FullNameRequired);
    }

    [Fact]
    public void Create_NameOver200Characters_Fails()
    {
        Member.Create(new string('ا', 201), Phone, null).Error.ShouldBe(MemberErrors.FullNameTooLong);
    }

    [Fact]
    public void Create_NotesOver1000Characters_Fails()
    {
        Member.Create("رضا", Phone, new string('x', 1001)).Error.ShouldBe(MemberErrors.NotesTooLong);
    }

    [Fact]
    public void Create_BlankNotes_AreStoredAsNull()
    {
        Member.Create("رضا", Phone, "   ").Value.Notes.ShouldBeNull();
    }

    [Theory]
    [InlineData("09121234567")]
    [InlineData("+98 912 123 4567")]
    public void Create_PhoneNotInE164_Throws(string phone)
    {
        // Normalizing is the caller's job (IPhoneNormalizer); skipping it is a bug, not input.
        Should.Throw<ArgumentException>(() => Member.Create("رضا", phone, null));
    }

    [Fact]
    public void Update_NewName_KeepsTheSearchColumnInStep()
    {
        var member = Member.Create("رضا", Phone, null).Value;

        // "Karim" typed with the Arabic kaf and the Arabic ye.
        member.Update($"{ArabicKaf}ر{ArabicYe}م", "+989351234567", "یادداشت").IsSuccess.ShouldBeTrue();

        member.NormalizedFullName.ShouldBe("کریم");
        member.PhoneNumber.ShouldBe("+989351234567");
        member.Notes.ShouldBe("یادداشت");
    }

    [Fact]
    public void Update_InvalidName_ChangesNothing()
    {
        var member = Member.Create("رضا", Phone, "قبلی").Value;

        member.Update(" ", "+989351234567", null).IsFailure.ShouldBeTrue();

        member.FullName.ShouldBe("رضا");
        member.PhoneNumber.ShouldBe(Phone);
        member.Notes.ShouldBe("قبلی");
    }

    [Fact]
    public void Deactivate_ActiveMember_BecomesInactive()
    {
        var member = Member.Create("رضا", Phone, null).Value;

        member.Deactivate();

        member.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_InactiveMember_StaysInactive()
    {
        var member = Member.Create("رضا", Phone, null).Value;
        member.Deactivate();

        member.Deactivate();

        member.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Reactivate_InactiveMember_BecomesActive()
    {
        var member = Member.Create("رضا", Phone, null).Value;
        member.Deactivate();

        member.Reactivate();

        member.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Reactivate_ActiveMember_StaysActive()
    {
        var member = Member.Create("رضا", Phone, null).Value;

        member.Reactivate();

        member.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Update_InactiveMember_SucceedsAndStaysInactive()
    {
        var member = Member.Create("رضا", Phone, null).Value;
        member.Deactivate();

        member.Update("رضا احمدی", "+989351234567", null).IsSuccess.ShouldBeTrue();

        member.FullName.ShouldBe("رضا احمدی");
        member.IsActive.ShouldBeFalse();
    }
}
