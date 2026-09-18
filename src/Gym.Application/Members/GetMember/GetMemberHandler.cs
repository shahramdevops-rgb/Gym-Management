using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.GetMember;

/// <summary>One member, active or not.</summary>
public sealed class GetMemberHandler(IAppDbContext db)
{
    public async Task<Result<MemberResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        // A read changes nothing, so nothing needs tracking: select the response straight from SQL.
        var member = await db.Members
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(MemberResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return member is null ? Result.Failure<MemberResponse>(MemberErrors.NotFound) : member;
    }
}
