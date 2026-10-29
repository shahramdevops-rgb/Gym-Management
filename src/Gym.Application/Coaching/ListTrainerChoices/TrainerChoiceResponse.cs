namespace Gym.Application.Coaching.ListTrainerChoices;

/// <summary>A trainer on the member's list, to pick.</summary>
public sealed record TrainerChoiceResponse(Guid TrainerId, string FullName);
