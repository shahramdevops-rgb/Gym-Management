using System.Text.Json.Serialization;

using Gym.Domain.Notifications;

namespace Gym.Application.Notifications;

/// <summary>
/// One SMS as the history shows it (BUSINESS_RULES.md §10, task 10.5): what was sent, to whom, and
/// how it went. The same shape answers a resend, so the page replaces the row it shows.
/// </summary>
/// <remarks>
/// The enums go out by name, as the list's filters take them; the converters sit here because the
/// Domain's enums know nothing of JSON.
/// </remarks>
/// <param name="MemberId">
/// The member it was for, to open their profile; <c>null</c> for the Owner's and for the trainer's
/// SMS (task 15.4c), which goes to a trainer.
/// </param>
/// <param name="MemberName">
/// Who it was for: the member, or the trainer for the trainer's SMS; <c>null</c> for a cheque or
/// instalment, sent to the Owner.
/// </param>
/// <param name="BroadcastId">The broadcast it was part of (task 10.13); <c>null</c> for every other kind.</param>
/// <param name="AutoResends">How many times the system sent it again by itself after the provider cancelled it (task 10.16).</param>
/// <param name="ErrorCode">The provider's code for the last failure; the page explains the known ones.</param>
/// <param name="CostToman">What the provider charged, in Toman (its Rial ÷ 10); <c>null</c> until it is sent.</param>
/// <param name="CreatedAt">When the message was written: its date in the history.</param>
public sealed record SmsMessageResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<NotificationKind>))] NotificationKind Kind,
    [property: JsonConverter(typeof(JsonStringEnumConverter<NotificationStatus>))] NotificationStatus Status,
    string Recipient,
    Guid? MemberId,
    string? MemberName,
    Guid? PayableId,
    Guid? BroadcastId,
    string Text,
    int Attempts,
    int AutoResends,
    int? ErrorCode,
    decimal? CostToman,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SmsDelivery>))] SmsDelivery? Delivery,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? SentAt)
{
    public static SmsMessageResponse From(Notification notification, string? memberName)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new SmsMessageResponse(
            notification.Id,
            notification.Kind,
            notification.Status,
            notification.Recipient,
            notification.Kind == NotificationKind.TrainerRequest ? null : notification.MemberId,
            memberName,
            notification.PayableId,
            notification.BroadcastId,
            notification.Text,
            notification.Attempts,
            notification.AutoResends,
            notification.ErrorCode,
            notification.CostRial / 10m,
            notification.Delivery,
            notification.CreatedAt,
            notification.LastAttemptAt,
            notification.SentAt);
    }
}
