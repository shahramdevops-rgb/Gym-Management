using Gym.Domain.Shop;

namespace Gym.Domain.Tests.Shop;

/// <summary>The shop's contact ways (BUSINESS_RULES.md §16 <i>The shop admin</i>).</summary>
public sealed class ShopSettingsTests
{
    private static readonly ShopContacts Filled = new(
        MobilePhone: "+989121234567",
        LandlinePhone: "07132345678",
        WhatsAppPhone: "+989351234567",
        Telegram: "pasargad_gym",
        Rubika: "pasargadgym",
        Eitaa: "pasargad.gym",
        Bale: "pasargadgym",
        Instagram: "pasargad.gym.plus");

    private static readonly ShopContacts NoneGiven = new(null, null, null, null, null, null, null, null);

    [Fact]
    public void SetContacts_EveryWayInShape_KeepsThemAll()
    {
        var settings = Empty();

        var result = settings.SetContacts(Filled);

        result.IsSuccess.ShouldBeTrue();
        settings.MobilePhone.ShouldBe("+989121234567");
        settings.LandlinePhone.ShouldBe("07132345678");
        settings.WhatsAppPhone.ShouldBe("+989351234567");
        settings.Telegram.ShouldBe("pasargad_gym");
        settings.Rubika.ShouldBe("pasargadgym");
        settings.Eitaa.ShouldBe("pasargad.gym");
        settings.Bale.ShouldBe("pasargadgym");
        settings.Instagram.ShouldBe("pasargad.gym.plus");
    }

    [Fact]
    public void SetContacts_NoneGiven_ClearsEveryWay()
    {
        var settings = Empty();
        settings.SetContacts(Filled).IsSuccess.ShouldBeTrue();

        var result = settings.SetContacts(NoneGiven);

        result.IsSuccess.ShouldBeTrue();
        settings.MobilePhone.ShouldBeNull();
        settings.LandlinePhone.ShouldBeNull();
        settings.Telegram.ShouldBeNull();
        settings.Instagram.ShouldBeNull();
    }

    public static TheoryData<string, string> OutOfShape => new()
    {
        { nameof(ShopContacts.MobilePhone), "09121234567" },
        { nameof(ShopContacts.WhatsAppPhone), "+14155550100" },
        { nameof(ShopContacts.LandlinePhone), "7132345678" },
        { nameof(ShopContacts.LandlinePhone), "09121234567" },
        { nameof(ShopContacts.LandlinePhone), "071-3234-5678" },
        { nameof(ShopContacts.LandlinePhone), "07132345678\n" },
        { nameof(ShopContacts.MobilePhone), "+989121234567\n" },
        { nameof(ShopContacts.Telegram), "pasargad\n" },
        { nameof(ShopContacts.Telegram), "@pasargad" },
        { nameof(ShopContacts.Rubika), "ab" },
        { nameof(ShopContacts.Eitaa), new string('a', 33) },
        { nameof(ShopContacts.Bale), "پاسارگاد" },
        { nameof(ShopContacts.Instagram), "https://instagram.com/gym" },
    };

    [Theory]
    [MemberData(nameof(OutOfShape))]
    public void SetContacts_AWayOutOfShape_IsRefusedAndNothingChanges(string way, string value)
    {
        var settings = Empty();
        settings.SetContacts(Filled).IsSuccess.ShouldBeTrue();

        var result = settings.SetContacts(With(way, value));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ShopErrors.ContactInvalid);
        settings.MobilePhone.ShouldBe("+989121234567", "a refused save keeps every way as it was.");
        settings.Telegram.ShouldBe("pasargad_gym");
    }

    private static ShopContacts With(string way, string value) => way switch
    {
        nameof(ShopContacts.MobilePhone) => NoneGiven with { MobilePhone = value },
        nameof(ShopContacts.WhatsAppPhone) => NoneGiven with { WhatsAppPhone = value },
        nameof(ShopContacts.LandlinePhone) => NoneGiven with { LandlinePhone = value },
        nameof(ShopContacts.Telegram) => NoneGiven with { Telegram = value },
        nameof(ShopContacts.Rubika) => NoneGiven with { Rubika = value },
        nameof(ShopContacts.Eitaa) => NoneGiven with { Eitaa = value },
        nameof(ShopContacts.Bale) => NoneGiven with { Bale = value },
        nameof(ShopContacts.Instagram) => NoneGiven with { Instagram = value },
        _ => throw new ArgumentOutOfRangeException(nameof(way), way, "Not a contact way."),
    };

    private static ShopSettings Empty() => (ShopSettings)Activator.CreateInstance(typeof(ShopSettings), nonPublic: true)!;
}
