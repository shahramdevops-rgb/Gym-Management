using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Trainers.ListTrainers;

/// <summary>
/// Every trainer, stopped ones included, for «مربیان» (BUSINESS_RULES.md §15): trainers now first,
/// then by name. A gym has a handful, so there is no paging.
/// </summary>
public sealed class ListTrainersHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<TrainerResponse>> Handle(CancellationToken cancellationToken)
    {
        var trainers = await db.Trainers.Include(row => row.Periods).AsNoTracking().ToListAsync(cancellationToken);

        var lastSignIns = await db.SiteAccounts
            .Where(account => account.TrainerId != null)
            .ToDictionaryAsync(account => account.TrainerId!.Value, account => account.LastSignedInAt, cancellationToken);

        return
        [
            .. trainers
                .OrderByDescending(trainer => trainer.IsActive)
                .ThenBy(trainer => trainer.FullName, StringComparer.Ordinal)
                .Select(trainer => TrainerResponse.From(
                    trainer,
                    lastSignIns.TryGetValue(trainer.Id, out var signedInAt) ? signedInAt : null)),
        ];
    }
}
