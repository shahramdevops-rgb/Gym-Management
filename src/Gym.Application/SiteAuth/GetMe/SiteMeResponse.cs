using Gym.Domain.Members;
using Gym.Domain.Trainers;

namespace Gym.Application.SiteAuth.GetMe;

/// <summary>
/// Who signed in on the site (BUSINESS_RULES.md §15): the member's area, the trainer's, or both
/// when one person is both with one phone. Neither is possible too: a trainer who was stopped and
/// is not a member, whom the site tells so.
/// </summary>
public sealed record SiteMeResponse(SiteMemberMe? Member, SiteTrainerMe? Trainer)
{
    public static SiteMeResponse From(SitePerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return new SiteMeResponse(
            person.Member is { } member ? SiteMemberMe.From(member) : null,
            person.IsTrainer ? SiteTrainerMe.From(person.Trainer!) : null);
    }
}

/// <summary>
/// The member as the site shows them: the panel's own details, never changed from the site.
/// <see cref="IsActive"/> false is the «غیرفعال» notice, and the member's area then shows nothing else.
/// </summary>
public sealed record SiteMemberMe(Guid MemberId, string FullName, string PhoneNumber, DateOnly BirthDate, bool IsActive)
{
    public static SiteMemberMe From(Member member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new SiteMemberMe(member.Id, member.FullName, member.PhoneNumber, member.BirthDate, member.IsActive);
    }
}

/// <summary>The trainer as the Owner entered them in «مربیان»; only while they are a trainer.</summary>
public sealed record SiteTrainerMe(Guid TrainerId, string FullName, string PhoneNumber)
{
    public static SiteTrainerMe From(Trainer trainer)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        return new SiteTrainerMe(trainer.Id, trainer.FullName, trainer.PhoneNumber);
    }
}
