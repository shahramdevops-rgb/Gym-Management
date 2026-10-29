using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Coaching.GetStudents;

/// <summary>The trainer's requests waiting for an answer and «شاگردان من» (BUSINESS_RULES.md §15).</summary>
public sealed class GetStudentsHandler(IAppDbContext db)
{
    public async Task<Result<StudentsResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentsResponse>(trainer.Error);
        }

        return await StudentsResponse.ForAsync(db, trainer.Value.Id, cancellationToken);
    }
}
