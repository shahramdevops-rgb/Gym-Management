namespace Gym.Application.Auth.ChangePassword;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);
