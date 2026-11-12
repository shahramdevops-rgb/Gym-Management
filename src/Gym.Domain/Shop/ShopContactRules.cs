using System.Text.RegularExpressions;

namespace Gym.Domain.Shop;

/// <summary>
/// The shape of each contact way (BUSINESS_RULES.md §16 <i>The shop admin</i>). The database's check
/// constraints repeat these patterns, so each is a public constant they can quote.
/// </summary>
/// <remarks>
/// <para>Null means "not given" and passes every check: each way is optional.</para>
/// <para>
/// The constants are the database's patterns. The checks here use the same patterns ending in
/// <c>\z</c>, because .NET's <c>$</c> also matches before a final line break and Postgres' does not:
/// a landline ending in a line break would pass here and fail there.
/// </para>
/// </remarks>
public static partial class ShopContactRules
{
    /// <summary>An Iranian mobile as every mobile is kept (§2): E.164, <c>+989</c> and nine digits.</summary>
    public const string MobilePattern = @"^\+989[0-9]{9}$";

    /// <summary>
    /// An Iranian landline with its area code: <c>0</c>, an area code from 1 to 8 and nine more
    /// digits, eleven in all (<c>07132345678</c>).
    /// </summary>
    public const string LandlinePattern = "^0[1-8][0-9]{9}$";

    /// <summary>
    /// A messenger's id without the <c>@</c>: Latin letters, digits, <c>_</c> and <c>.</c>, 3 to 32 of
    /// them. Wide enough for Telegram, Rubika, Eitaa, Bale and Instagram alike; each checks its own
    /// finer rules when the id is made, so the shop only needs to keep out what is plainly not an id.
    /// </summary>
    public const string HandlePattern = "^[A-Za-z0-9_.]{3,32}$";

    /// <summary>The longest value any column holds: a handle's 32.</summary>
    public const int MaxLength = 32;

    public static bool IsMobile(string? value) => value is null || Mobile().IsMatch(value);

    public static bool IsLandline(string? value) => value is null || Landline().IsMatch(value);

    public static bool IsHandle(string? value) => value is null || Handle().IsMatch(value);

    [GeneratedRegex(@"^\+989[0-9]{9}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Mobile();

    [GeneratedRegex(@"^0[1-8][0-9]{9}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Landline();

    [GeneratedRegex(@"^[A-Za-z0-9_.]{3,32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Handle();
}
