namespace Gym.Application.Coaching.AcceptTrainerRequest;

/// <summary>«قبول» with the coaching period's days the trainer typed (15.7h); <c>null</c> is <c>CoachingPeriod.DefaultDays</c>.</summary>
public sealed record AcceptTrainerRequestCommand(int? Days);
