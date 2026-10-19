namespace Gym.Application.Notifications.Broadcasts.PreviewBroadcast;

/// <param name="Recipients">How many members it would go to now.</param>
/// <param name="LongestMessageParts">The parts of the longest message, the one with the longest first name.</param>
/// <param name="TotalParts">Every recipient's own message counted: what the provider will charge for.</param>
/// <param name="EstimatedCostToman">
/// <c>null</c> until some message has been sent and charged: the provider tells no price beforehand,
/// so the estimate comes from the latest one (§10 <i>The broadcast</i>).
/// </param>
/// <param name="SampleText">The whole message to the recipient with the longest first name, as it will reach the phone.</param>
public sealed record BroadcastPreviewResponse(
    int Recipients,
    int LongestMessageParts,
    int TotalParts,
    decimal? EstimatedCostToman,
    string SampleText);
