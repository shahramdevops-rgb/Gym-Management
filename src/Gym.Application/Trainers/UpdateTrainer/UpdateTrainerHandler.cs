using Gym.Application.Common;
using Gym.Application.SiteAuth;
using Gym.Domain.Auth;
using Gym.Domain.Common;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Trainers.UpdateTrainer;

/// <summary>
/// The Owner corrects a trainer's name or phone (BUSINESS_RULES.md §15), stopped trainers too. The
/// form's <c>Version</c> refuses a stale edit, and <c>xmin</c> the moment between read and write,
/// as for a member.
/// </summary>
/// <remarks>
/// <b>A new phone takes the trainer off the old phone's account.</b> Whoever signed in with the old
/// number is no longer the trainer: the account's trainer link is cleared, and when that leaves the
/// account with no member either, its sessions end in the same save. A member on that account keeps
/// their session and loses the trainer's area at the next refresh. The trainer signs in again with
/// the new number.
/// </remarks>
public sealed class UpdateTrainerHandler(IAppDbContext db, IPhoneNormalizer phones, TimeProvider timeProvider)
{
    public async Task<Result<TrainerResponse>> Handle(Guid id, UpdateTrainerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trainer = await db.Trainers.Include(row => row.Periods).SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (trainer is null)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.NotFound);
        }

        if (trainer.Version != command.Version)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.ChangedConcurrently);
        }

        var phone = phones.Normalize(command.PhoneNumber);
        if (phone.IsFailure)
        {
            return Result.Failure<TrainerResponse>(phone.Error);
        }

        // Keeping one's own number is fine; taking another trainer's is not.
        if (await db.Trainers.AnyAsync(row => row.PhoneNumber == phone.Value && row.Id != id, cancellationToken))
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.PhoneAlreadyExists);
        }

        var oldPhone = trainer.PhoneNumber;
        var phoneChanged = oldPhone != phone.Value;

        var updated = trainer.Update(command.FullName, phone.Value);
        if (updated.IsFailure)
        {
            return Result.Failure<TrainerResponse>(updated.Error);
        }

        // A sign-in with the old phone may be finishing at this moment: the phone's lock waits for
        // it, so the account it links is found and cleared here instead of keeping the trainer.
        await using var transaction = phoneChanged ? await db.BeginTransactionAsync(cancellationToken) : null;
        if (phoneChanged)
        {
            await db.LockPhoneAsync(oldPhone, cancellationToken);
            await TakeOffTheOldPhonesAccountAsync(id, cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == TrainerConstraints.UniquePhone)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.PhoneAlreadyExists);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.ChangedConcurrently);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return await TrainerResponse.ForAsync(db, trainer, cancellationToken);
    }

    /// <summary>Clears the account's trainer link, and ends its sessions if nothing else is left on it.</summary>
    private async Task TakeOffTheOldPhonesAccountAsync(Guid trainerId, CancellationToken cancellationToken)
    {
        var account = await db.SiteAccounts.SingleOrDefaultAsync(row => row.TrainerId == trainerId, cancellationToken);
        if (account is null)
        {
            return;
        }

        account.UnlinkTrainer();

        if (account.MemberId is null)
        {
            var accountId = account.Id;
            await db.RevokeSiteSessionsAsync(
                db.SiteAccounts.Where(row => row.Id == accountId).Select(row => row.Id),
                RefreshTokenRevocationReason.PhoneChanged,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
    }
}
