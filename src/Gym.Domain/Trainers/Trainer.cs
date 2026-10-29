using Gym.Domain.Common;

namespace Gym.Domain.Trainers;

/// <summary>
/// A trainer of the gym (BUSINESS_RULES.md §15 <i>Trainers</i>): made by the Owner in the panel,
/// signs in on the site with <see cref="PhoneNumber"/>. Not a member, though the same person may
/// also be one, with the same phone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stopped, never deleted.</b> Being a trainer is a list of <see cref="Periods"/>: the Owner
/// stopping it closes the open one, making them a trainer again opens a new one. The trainer stays
/// one row, so what they write on the site (their exercises, templates and programs) is still
/// theirs when they come back.
/// </para>
/// <para>
/// The phone arrives already in E.164, as <c>Member</c>'s does: <c>IPhoneNormalizer</c> in the
/// Application layer turns what the Owner typed into that form.
/// </para>
/// </remarks>
public sealed class Trainer : Entity
{
    public const int FullNameMaxLength = 200;

    private readonly List<TrainerPeriod> _periods = [];

    // For EF Core.
    private Trainer()
    {
    }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>E.164. Unique among trainers, stopped ones included.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>Postgres <c>xmin</c>: the second of two edits at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    /// <summary>Oldest first. At most one is open, and only the last.</summary>
    public IReadOnlyList<TrainerPeriod> Periods => _periods;

    /// <summary>A trainer now: their last period is still open.</summary>
    public bool IsActive => _periods.Any(period => period.IsOpen);

    /// <param name="today">The gym's today: the first period starts on it.</param>
    public static Result<Trainer> Create(string fullName, string phoneNumber, DateOnly today)
    {
        var trainer = new Trainer();
        var result = trainer.Update(fullName, phoneNumber);
        if (result.IsFailure)
        {
            return Result.Failure<Trainer>(result.Error);
        }

        trainer._periods.Add(TrainerPeriod.Start(trainer.Id, today));

        return trainer;
    }

    /// <summary>Corrects the name or the phone; allowed while stopped too.</summary>
    public Result Update(string fullName, string phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        if (!IsE164(phoneNumber))
        {
            // A caller that skipped IPhoneNormalizer is a bug, not bad input from a user.
            throw new ArgumentException("The phone number must already be normalized to E.164.", nameof(phoneNumber));
        }

        var name = fullName.Trim();
        if (name.Length == 0)
        {
            return Result.Failure(TrainerErrors.FullNameRequired);
        }

        if (name.Length > FullNameMaxLength)
        {
            return Result.Failure(TrainerErrors.FullNameTooLong);
        }

        FullName = name;
        PhoneNumber = phoneNumber;

        return Result.Success();
    }

    /// <summary>
    /// The Owner stops someone being a trainer: the open period ends today. Their students' links
    /// end with it (15.4); everything recorded about the students stays.
    /// </summary>
    public Result Stop(DateOnly today)
    {
        var open = _periods.SingleOrDefault(period => period.IsOpen);
        if (open is null)
        {
            return Result.Failure(TrainerErrors.AlreadyStopped);
        }

        // A clock that went back would end a period before it began; the database refuses that
        // too, but the domain says it first.
        open.End(today < open.StartedOn ? open.StartedOn : today);

        return Result.Success();
    }

    /// <summary>
    /// The Owner makes a stopped trainer a trainer again: a new period starts today. Former
    /// students are not linked back; they pick a trainer again on the site.
    /// </summary>
    public Result Restart(DateOnly today)
    {
        if (IsActive)
        {
            return Result.Failure(TrainerErrors.AlreadyActive);
        }

        _periods.Add(TrainerPeriod.Start(Id, today));

        return Result.Success();
    }

    private static bool IsE164(string value) =>
        value.Length is >= 8 and <= 16 && value[0] == '+' && value.Skip(1).All(char.IsAsciiDigit);
}
