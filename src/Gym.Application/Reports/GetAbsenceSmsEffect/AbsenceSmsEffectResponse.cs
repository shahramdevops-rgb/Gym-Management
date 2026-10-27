using System.Text.Json.Serialization;

using Gym.Domain.Notifications;

namespace Gym.Application.Reports.GetAbsenceSmsEffect;

/// <summary>
/// Every absence SMS since the first and what became of each (BUSINESS_RULES.md §12 <i>The
/// absence SMS's effect</i>, task 10.19).
/// </summary>
/// <param name="From">The first message's day (today when there is none).</param>
/// <param name="To">Today.</param>
/// <param name="Sent">The absence SMS that reached, or may have reached, the phone.</param>
/// <param name="CameBack">Of <paramref name="Sent"/>, those whose member checked in within 5 days.</param>
/// <param name="Waiting">Of <paramref name="Sent"/>, those not come back yet whose 5 days are not over.</param>
/// <param name="DidNotComeBack">Of <paramref name="Sent"/>, those whose 5 days are over with no visit.</param>
/// <param name="NotMeasurable">
/// Of <paramref name="Sent"/>, those whose plan ended inside the window, was cancelled, or was frozen
/// before the member came. The rate is <paramref name="CameBack"/> ÷ (<paramref name="CameBack"/> +
/// <paramref name="DidNotComeBack"/>).
/// </param>
/// <param name="AverageDaysToComeBack">Of those who came back, the average days, to one decimal; <c>null</c> when none did.</param>
/// <param name="CameBackByDay">Those who came back, by days after the message: day 0 to day 5, six numbers.</param>
/// <param name="CostToman">What <paramref name="Sent"/> cost, as the provider charged it.</param>
/// <param name="CostPerReturnToman">That cost divided by <paramref name="CameBack"/>, to the Toman; <c>null</c> when nobody came back.</param>
/// <param name="Messages">
/// Each message: those who came back first, then those waiting, those who did not, and those not
/// measurable; the latest sent first in each.
/// </param>
public sealed record AbsenceSmsEffectResponse(
    DateOnly From,
    DateOnly To,
    int Sent,
    int CameBack,
    int Waiting,
    int DidNotComeBack,
    int NotMeasurable,
    decimal? AverageDaysToComeBack,
    IReadOnlyList<int> CameBackByDay,
    decimal CostToman,
    decimal? CostPerReturnToman,
    IReadOnlyList<AbsenceSmsMessageResponse> Messages);

/// <param name="SentOn">The gym's day the message was sent.</param>
/// <param name="DaysToComeBack">Set when the member came back: days from <paramref name="SentOn"/> to the visit.</param>
/// <param name="DaysLeft">Set while waiting: the days still left of the 5, 0 on the last one.</param>
public sealed record AbsenceSmsMessageResponse(
    Guid NotificationId,
    Guid MemberId,
    string FullName,
    DateOnly SentOn,
    [property: JsonConverter(typeof(JsonStringEnumConverter<AbsenceSmsOutcome>))] AbsenceSmsOutcome Outcome,
    int? DaysToComeBack,
    int? DaysLeft);
