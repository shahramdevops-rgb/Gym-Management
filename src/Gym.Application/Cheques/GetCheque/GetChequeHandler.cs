using Gym.Application.Common;
using Gym.Domain.Cheques;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cheques.GetCheque;

/// <summary>One cheque, whatever its status.</summary>
public sealed class GetChequeHandler(IAppDbContext db)
{
    public async Task<Result<ChequeResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var cheque = await db.Cheques.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        return cheque is null
            ? Result.Failure<ChequeResponse>(ChequeErrors.NotFound)
            : ChequeResponse.From(cheque);
    }
}
