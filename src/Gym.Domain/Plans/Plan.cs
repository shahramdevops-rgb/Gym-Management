using Gym.Domain.Common;
using Gym.Domain.Common.Text;

namespace Gym.Domain.Plans;

/// <summary>
/// Something the gym sells: "30 days, 12 sessions, 900,000" (BUSINESS_RULES.md §3).
/// Plans are deactivated, never deleted.
/// </summary>
/// <remarks>
/// <para>
/// A subscription copies the plan's name, price, duration and session count when it is sold
/// (§4), so editing or deactivating a plan never reaches a subscription already sold. That is
/// why this class needs no knowledge of subscriptions at all.
/// </para>
/// <para>
/// <see cref="SessionCount"/> is nullable on purpose: <c>null</c> means unlimited. A sentinel
/// such as 0 or -1 would be a number every caller must remember is not a number.
/// </para>
/// </remarks>
public sealed class Plan : Entity
{
    public const int NameMaxLength = 100;

    /// <summary>Decided with the developer in task 3.1: no plan is longer than a year.</summary>
    public const int MaxDurationDays = 365;

    /// <summary>Decided with the developer in task 3.1: at most one session a day for a year.</summary>
    public const int MaxSessionCount = 365;

    /// <summary>The column is <c>numeric(18,2)</c>: 16 digits before the point, 2 after.</summary>
    public const int PriceDecimals = 2;

    public const decimal MaxPrice = 9_999_999_999_999_999.99m;

    // For EF Core.
    private Plan()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// <see cref="Name"/> through <see cref="PersianText.Normalize"/>, in lower case. The unique
    /// index is on this column, so "پلن ویژه" typed with an Arabic ye is still a duplicate.
    /// </summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public int DurationDays { get; private set; }

    /// <summary><c>null</c> means unlimited sessions within <see cref="DurationDays"/>.</summary>
    public int? SessionCount { get; private set; }

    public decimal Price { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Postgres <c>xmin</c>, so a stale edit cannot overwrite a newer one.</summary>
    public uint Version { get; private set; }

    public bool IsUnlimited => SessionCount is null;

    public static Result<Plan> Create(string name, int durationDays, int? sessionCount, decimal price)
    {
        var plan = new Plan { IsActive = true };
        var result = plan.Update(name, durationDays, sessionCount, price);

        return result.IsSuccess ? plan : Result.Failure<Plan>(result.Error);
    }

    /// <summary>
    /// Replaces the plan's details. Allowed while inactive too. Subscriptions already sold keep
    /// their own copy of these values (BUSINESS_RULES.md §3).
    /// </summary>
    public Result Update(string name, int durationDays, int? sessionCount, decimal price)
    {
        ArgumentNullException.ThrowIfNull(name);

        var cleanName = name.Trim();
        if (cleanName.Length == 0)
        {
            return Result.Failure(PlanErrors.NameRequired);
        }

        if (cleanName.Length > NameMaxLength)
        {
            return Result.Failure(PlanErrors.NameTooLong);
        }

        if (durationDays is < 1 or > MaxDurationDays)
        {
            return Result.Failure(PlanErrors.DurationInvalid);
        }

        if (sessionCount is < 1 or > MaxSessionCount)
        {
            return Result.Failure(PlanErrors.SessionCountInvalid);
        }

        var priceError = CheckPrice(price);
        if (priceError is not null)
        {
            return Result.Failure(priceError);
        }

        Name = cleanName;
        NormalizedName = PersianText.Normalize(cleanName).ToLowerInvariant();
        DurationDays = durationDays;
        SessionCount = sessionCount;
        Price = price;

        return Result.Success();
    }

    /// <summary>Deactivating an inactive plan succeeds and changes nothing.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Activating an active plan succeeds and changes nothing.</summary>
    public void Activate() => IsActive = true;

    /// <summary>
    /// BUSINESS_RULES.md §3: inactive plans cannot be sold. The use case that sells a plan
    /// (task 4.2) asks this instead of reading <see cref="IsActive"/> itself, so the rule has
    /// one home.
    /// </summary>
    public Result EnsureCanBeSold() => IsActive ? Result.Success() : Result.Failure(PlanErrors.Inactive);

    /// <summary>
    /// Also used by the Application validators, so the form hears the same answer as the entity.
    /// Too many decimals is refused rather than rounded: rounding money silently is how
    /// "900,000.005" becomes a price nobody typed.
    /// </summary>
    public static Error? CheckPrice(decimal price)
    {
        if (price < 0)
        {
            return PlanErrors.PriceNegative;
        }

        if (price > MaxPrice)
        {
            return PlanErrors.PriceTooLarge;
        }

        return decimal.Round(price, PriceDecimals) != price ? PlanErrors.PriceTooManyDecimals : null;
    }
}
