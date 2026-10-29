using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Trainers.CreateTrainer;

/// <summary>
/// The Owner makes someone a trainer, with a name and a phone (BUSINESS_RULES.md §15 <i>Trainers</i>).
/// The first period starts today. The phone may be a member's: one person can be both.
/// </summary>
public sealed class CreateTrainerHandler(IAppDbContext db, IPhoneNormalizer phones, IGymCalendar calendar)
{
    public async Task<Result<TrainerResponse>> Handle(CreateTrainerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var phone = phones.Normalize(command.PhoneNumber);
        if (phone.IsFailure)
        {
            return Result.Failure<TrainerResponse>(phone.Error);
        }

        // Stopped trainers count: their phone stays theirs, for when they come back.
        if (await db.Trainers.AnyAsync(trainer => trainer.PhoneNumber == phone.Value, cancellationToken))
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.PhoneAlreadyExists);
        }

        var created = Trainer.Create(command.FullName, phone.Value, calendar.Today());
        if (created.IsFailure)
        {
            return Result.Failure<TrainerResponse>(created.Error);
        }

        // The check above can pass for two requests at once; the unique index decides.
        db.Trainers.Add(created.Value);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == TrainerConstraints.UniquePhone)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.PhoneAlreadyExists);
        }

        // A new trainer has never signed in: an account of theirs is made at their first sign-in.
        return TrainerResponse.From(created.Value, lastSignedInAt: null);
    }
}
