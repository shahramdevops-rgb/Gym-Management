using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Payables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Payables.GetPayable;

/// <summary>One cheque or instalment, whatever its status.</summary>
public sealed class GetPayableHandler(IAppDbContext db)
{
    public async Task<Result<PayableResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var payable = await db.Payables.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

        return payable is null
            ? Result.Failure<PayableResponse>(PayableErrors.NotFound)
            : await PayableResponses.ForAsync(db, payable, cancellationToken);
    }
}
