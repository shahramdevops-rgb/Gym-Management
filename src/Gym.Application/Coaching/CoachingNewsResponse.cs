using System.Text.Json.Serialization;

using Gym.Application.Common;
using Gym.Domain.Coaching;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching;

/// <summary>
/// The card «خبر مربی» on a member's home, or «خبر شاگردان» on a trainer's (BUSINESS_RULES.md §15
/// <i>A member and their trainer</i>, task 15.4d): what the other side did that they have not
/// pressed «دیدم» for, from the last <see cref="CoachingNews.ShownForDays"/> days, newest first.
/// </summary>
/// <param name="Items">Empty when there is nothing to tell.</param>
public sealed record CoachingNewsResponse(IReadOnlyList<CoachingNewsItemResponse> Items)
{
    /// <summary>
    /// The member's: their trainer's answers, and the links their trainer ended. An acceptance is
    /// left out once its link has ended: what came after is the news, or nobody's.
    /// </summary>
    internal static async Task<CoachingNewsResponse> ForMemberAsync(IAppDbContext db, Guid memberId, DateOnly today, CancellationToken cancellationToken)
    {
        var since = CoachingNews.FirstShownDay(today);

        var answers = await db.TrainerRequests.AsNoTracking()
            .Where(request => request.MemberId == memberId
                && request.AnsweredOn != null && request.AnsweredOn >= since
                && request.AnswerSeenAt == null
                && (request.Status == TrainerRequestStatus.Declined
                    || (request.Status == TrainerRequestStatus.Accepted
                        && db.TrainerLinks.Any(link => link.RequestId == request.Id && link.EndedOn == null))))
            .Join(db.Trainers, request => request.TrainerId, trainer => trainer.Id,
                (request, trainer) => new CoachingNewsItemResponse(
                    request.Id,
                    request.Status == TrainerRequestStatus.Accepted ? CoachingNewsKind.Accepted : CoachingNewsKind.Declined,
                    trainer.FullName,
                    request.AnsweredOn!.Value))
            .ToListAsync(cancellationToken);

        // CoachingNews.TellsMember: only the trainer's own end.
        var ends = await db.TrainerLinks.AsNoTracking()
            .Where(link => link.MemberId == memberId
                && link.EndReason == TrainerLinkEndReason.ByTrainer
                && link.EndedOn >= since
                && link.EndSeenAt == null)
            .Join(db.Trainers, link => link.TrainerId, trainer => trainer.Id,
                (link, trainer) => new CoachingNewsItemResponse(link.Id, CoachingNewsKind.EndedByTrainer, trainer.FullName, link.EndedOn!.Value))
            .ToListAsync(cancellationToken);

        return new CoachingNewsResponse(Newest(answers.Concat(ends)));
    }

    /// <summary>The trainer's: the students who ended their link.</summary>
    internal static async Task<CoachingNewsResponse> ForTrainerAsync(IAppDbContext db, Guid trainerId, DateOnly today, CancellationToken cancellationToken)
    {
        var since = CoachingNews.FirstShownDay(today);

        // CoachingNews.TellsTrainer: only the student's own end.
        var ends = await db.TrainerLinks.AsNoTracking()
            .Where(link => link.TrainerId == trainerId
                && link.EndReason == TrainerLinkEndReason.ByMember
                && link.EndedOn >= since
                && link.EndSeenAt == null)
            .Join(db.Members, link => link.MemberId, member => member.Id,
                (link, member) => new CoachingNewsItemResponse(link.Id, CoachingNewsKind.EndedByStudent, member.FullName, link.EndedOn!.Value))
            .ToListAsync(cancellationToken);

        return new CoachingNewsResponse(Newest(ends));
    }

    // Ordered here, not in SQL: two queries, and EF Core cannot order by a constructor's argument.
    private static List<CoachingNewsItemResponse> Newest(IEnumerable<CoachingNewsItemResponse> items) =>
        items.OrderByDescending(item => item.On).ThenBy(item => item.FullName, StringComparer.Ordinal).ToList();
}

/// <param name="Id">
/// The request's id for an answer, the link's for an end: what «دیدم» sends back.
/// </param>
/// <param name="FullName">The trainer's name on a member's card, the student's on a trainer's.</param>
/// <param name="On">The gym's day it happened.</param>
public sealed record CoachingNewsItemResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CoachingNewsKind>))] CoachingNewsKind Kind,
    string FullName,
    DateOnly On);

/// <summary>What happened, for the card's sentence.</summary>
public enum CoachingNewsKind
{
    /// <summary>To the member: the trainer accepted their request.</summary>
    Accepted,

    /// <summary>To the member: the trainer declined their request.</summary>
    Declined,

    /// <summary>To the member: the trainer ended the link.</summary>
    EndedByTrainer,

    /// <summary>To the trainer: the student ended the link.</summary>
    EndedByStudent,
}
