using Gym.Application.Common;
using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Notifications;

/// <summary>
/// Who each message in the SMS history went to, by name: the member for a member's kind, the trainer
/// for the trainer's SMS (task 15.4c), nobody for the Owner's. Two queries for a whole page.
/// </summary>
public static class SmsRecipientNames
{
    /// <returns>The name for each message that has one, by the message's id.</returns>
    public static async Task<Dictionary<Guid, string>> ForAsync(
        IAppDbContext db, IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(notifications);

        // The trainer's SMS also names the member it is about; it goes to the trainer.
        var memberIds = notifications
            .Where(notification => notification.TrainerId is null && notification.MemberId is not null)
            .Select(notification => notification.MemberId!.Value)
            .Distinct()
            .ToList();
        var trainerIds = notifications
            .Where(notification => notification.TrainerId is not null)
            .Select(notification => notification.TrainerId!.Value)
            .Distinct()
            .ToList();

        var members = memberIds.Count == 0
            ? []
            : await db.Members.AsNoTracking()
                .Where(member => memberIds.Contains(member.Id))
                .ToDictionaryAsync(member => member.Id, member => member.FullName, cancellationToken);
        var trainers = trainerIds.Count == 0
            ? []
            : await db.Trainers.AsNoTracking()
                .Where(trainer => trainerIds.Contains(trainer.Id))
                .ToDictionaryAsync(trainer => trainer.Id, trainer => trainer.FullName, cancellationToken);

        var names = new Dictionary<Guid, string>();
        foreach (var notification in notifications)
        {
            var name = notification switch
            {
                { TrainerId: { } trainerId } => trainers.GetValueOrDefault(trainerId),
                { MemberId: { } memberId } => members.GetValueOrDefault(memberId),
                _ => null,
            };
            if (name is not null)
            {
                names[notification.Id] = name;
            }
        }

        return names;
    }
}
