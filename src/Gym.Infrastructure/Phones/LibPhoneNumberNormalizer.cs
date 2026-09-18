using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Common.Text;
using Gym.Domain.Members;

using Microsoft.Extensions.Options;

using PhoneNumbers;

namespace Gym.Infrastructure.Phones;

/// <summary>The <c>Gym</c> configuration section's phone setting.</summary>
public sealed class PhoneOptions
{
    public const string SectionName = "Gym";

    /// <summary>
    /// BUSINESS_RULES.md §0: <c>IR</c>. The region a number without a country code belongs to,
    /// and, because the gym registers Iranian mobiles only, the only region accepted.
    /// </summary>
    public string PhoneDefaultRegion { get; set; } = "IR";
}

/// <summary>
/// <see cref="IPhoneNormalizer"/> with Google's libphonenumber, the library Android and most
/// phone software use. It knows every country's formats and which Iranian ranges are mobiles,
/// which is data no hand-written regular expression would keep up to date.
/// </summary>
public sealed class LibPhoneNumberNormalizer(IOptions<PhoneOptions> options) : IPhoneNormalizer
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    public Result<string> Normalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var region = options.Value.PhoneDefaultRegion;

        // Persian and Arabic digits first: libphonenumber understands some non-ASCII digits, but
        // the rule in BUSINESS_RULES.md is ours to guarantee, not a library detail to rely on.
        var digits = PersianText.NormalizeDigits(input).Trim();

        PhoneNumber number;
        try
        {
            number = Util.Parse(digits, region);
        }
        catch (NumberParseException)
        {
            return Result.Failure<string>(MemberErrors.PhoneInvalid);
        }

        if (!Util.IsValidNumber(number))
        {
            return Result.Failure<string>(MemberErrors.PhoneInvalid);
        }

        // BUSINESS_RULES.md §2: the gym has no foreign members.
        if (!string.Equals(Util.GetRegionCodeForNumber(number), region, StringComparison.Ordinal))
        {
            return Result.Failure<string>(MemberErrors.PhoneNotIranian);
        }

        // The number receives SMS reminders (Phase 10), so a landline is no use.
        if (Util.GetNumberType(number) != PhoneNumberType.MOBILE)
        {
            return Result.Failure<string>(MemberErrors.PhoneNotMobile);
        }

        return Util.Format(number, PhoneNumberFormat.E164);
    }
}
