namespace Gym.Api.Admin.Demo;

/// <summary>
/// Made-up people and things for the demo. No name or number here belongs to a real member: the
/// screenshots in the README come from this data, never from the gym's server.
/// </summary>
internal static class DemoNames
{
    public static readonly string[] FirstNames =
    [
        "علی", "محمد", "حسین", "رضا", "مهدی", "امیر", "سعید", "حمید", "مجید", "وحید",
        "پویا", "آرش", "کاوه", "بهزاد", "فرشید", "میلاد", "سجاد", "یاسر", "نوید", "پیمان",
        "کامران", "بابک", "سینا", "امید", "هادی", "مسعود", "جواد", "روزبه", "شهاب", "بردیا",
    ];

    public static readonly string[] LastNames =
    [
        "احمدی", "رضایی", "کریمی", "حسینی", "موسوی", "جعفری", "صادقی", "نوری", "کاظمی", "رحیمی",
        "قاسمی", "یزدانی", "شریفی", "عباسی", "طاهری", "فرهادی", "بهرامی", "سلطانی", "نیک‌نام", "مرادی",
        "زمانی", "اکبری", "تهرانی", "شیرازی", "پاکزاد", "امینی", "فاضلی", "خسروی", "کیانی", "اسدی",
    ];

    /// <summary>Iranian mobile prefixes, so every number passes the member phone rules (BUSINESS_RULES.md §2).</summary>
    public static readonly string[] MobilePrefixes = ["0912", "0919", "0935", "0936", "0901", "0910"];

    public static readonly string[] MemberNotes =
    [
        "آسیب‌دیدگی زانو؛ حرکات سبک پا",
        "برنامهٔ تمرینی را از مربی بگیرد",
        "دانشجو",
    ];

    public static readonly (string UserName, string FullName, string Password)[] Staff =
    [
        ("ali1405", "علی کریمی", "demo front desk morning 1405"),
        ("hossein1405", "حسین مرادی", "demo front desk evening 1405"),
    ];

    public static readonly (string Category, (string Name, decimal Price)[] Products)[] Menu =
    [
        ("نوشیدنی", [("آب معدنی", 15_000m), ("دلستر", 35_000m), ("نوشیدنی انرژی‌زا", 65_000m), ("قهوه", 55_000m)]),
        ("میان‌وعده", [("موز", 25_000m), ("بار پروتئینی", 80_000m), ("کیک خانگی", 45_000m)]),
        ("مکمل", [("شیک پروتئین", 140_000m), ("پیمانهٔ کراتین", 60_000m)]),
    ];

    public static readonly (string Description, decimal UnitPrice)[] ShopItems =
    [
        ("دستکش بدنسازی", 450_000m),
        ("شیکر", 180_000m),
        ("حوله", 250_000m),
    ];

    public static readonly string[] JalaliMonths =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
    ];
}
