using System.Text.Json.Serialization;

using Gym.Domain.Notifications;

namespace Gym.Application.Reports.GetAbsenceSmsEffect;

/// <summary>
/// The absence SMS sent in a range and what became of each (BUSINESS_RULES.md §12 <i>The absence
/// SMS's effect</i>, task 10.19).
/// </summary>
/// <param name="Sent">The absence SMS sent in the range that reached, or may have reached, the phone.</param>
/// <param name="CameBack">Of <paramref name="Sent"/>, those whose member checked in within 14 days.</param>
/// <param name="Waiting">
/// Of <paramref name="Sent"/>, those not come back yet whose 14 days are not over. The rate is
/// <paramref name="CameBack"/> ÷ (<paramref name="Sent"/> − <paramref name="Waiting"/>).
/// </param>
/// <param name="DidNotComeBack">Of <paramref name="Sent"/>, those whose 14 days are over with no visit.</param>
/// <param name="AverageDaysToComeBack">Of those who came back, the average days, to one decimal; <c>null</c> when none did.</param>
/// <param name="CostToman">What <paramref name="Sent"/> cost, as the provider charged it.</param>
/// <param name="Messages">
/// Each message: those who came back first, then those waiting, then those who did not; the
/// latest sent first in each.
/// </param>
public sealed record AbsenceSmsEffectResponse(
    DateOnly From,
    DateOnly To,
    int Sent,
    int CameBack,
    int Waiting,
    int DidNotComeBack,
    decimal? AverageDaysToComeBack,
    decimal CostToman,
    IReadOnlyList<AbsenceSmsMessageResponse> Messages);

/// <param name="SentOn">The gym's day the message was sent.</param>
/// <param name="DaysToComeBack">Set when the member came back: days from <paramref name="SentOn"/> to the visit.</param>
/// <param name="DaysLeft">Set while waiting: the days still left of the 14, 0 on the last one.</param>
public sealed record AbsenceSmsMessageResponse(
    Guid NotificationId,
    Guid MemberId,
    string FullName,
    DateOnly SentOn,
    [property: JsonConverter(typeof(JsonStringEnumConverter<AbsenceSmsOutcome>))] AbsenceSmsOutcome Outcome,
    int? DaysToComeBack,
    int? DaysLeft);
