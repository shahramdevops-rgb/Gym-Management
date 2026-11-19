namespace Gym.Application.Coaching.AcceptRenewalRequest;

/// <summary>«تمدید» from «درخواست‌ها»: <paramref name="Days"/> more days of coaching, as «تمدید» on «برنامهٔ ثابت».</summary>
public sealed record AcceptRenewalRequestCommand(int Days);
