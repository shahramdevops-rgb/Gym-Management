using Gym.Application.Common;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Trainers;

/// <summary>A trainer as «مربیان» shows them (BUSINESS_RULES.md §15).</summary>
/// <param name="Periods">Oldest first; the last is open while <see cref="IsActive"/>.</param>
/// <param name="LastSignedInAt">The trainer's last sign-in on the site; <c>null</c> before the first.</param>
/// <param name="Version">Sent back with an edit, so a stale form is refused.</param>
public sealed record TrainerResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    bool IsActive,
    IReadOnlyList<TrainerPeriodResponse> Periods,
    DateTimeOffset? LastSignedInAt,
    uint Version)
{
    public static TrainerResponse From(Trainer trainer, DateTimeOffset? lastSignedInAt)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        return new TrainerResponse(
            trainer.Id,
            trainer.FullName,
            trainer.PhoneNumber,
            trainer.IsActive,
            [.. trainer.Periods.OrderBy(period => period.StartedOn).ThenBy(period => period.Id).Select(TrainerPeriodResponse.From)],
            lastSignedInAt,
            trainer.Version);
    }

    /// <summary>The response for one trainer just saved, with their last sign-in read from their account.</summary>
    public static async Task<TrainerResponse> ForAsync(IAppDbContext db, Trainer trainer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(trainer);

        var lastSignedInAt = await db.SiteAccounts
            .Where(account => account.TrainerId == trainer.Id)
            .Select(account => (DateTimeOffset?)account.LastSignedInAt)
            .SingleOrDefaultAsync(cancellationToken);

        return From(trainer, lastSignedInAt);
    }
}

/// <param name="EndedOn"><c>null</c> while the period is open.</param>
public sealed record TrainerPeriodResponse(DateOnly StartedOn, DateOnly? EndedOn)
{
    public static TrainerPeriodResponse From(TrainerPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        return new TrainerPeriodResponse(period.StartedOn, period.EndedOn);
    }
}
