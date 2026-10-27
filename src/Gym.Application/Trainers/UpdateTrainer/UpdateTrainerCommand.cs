namespace Gym.Application.Trainers.UpdateTrainer;

/// <param name="Version">The <c>version</c> from the trainer as it was read before editing.</param>
public sealed record UpdateTrainerCommand(string FullName, string PhoneNumber, uint Version);
