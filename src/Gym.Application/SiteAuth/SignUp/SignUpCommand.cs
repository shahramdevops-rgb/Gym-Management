namespace Gym.Application.SiteAuth.SignUp;

/// <param name="Ticket">The sign-in's <c>SignUpTicket</c>, for the phone that just typed its code.</param>
/// <param name="BirthDate">Required (BUSINESS_RULES.md §2), Gregorian; nullable for the same reason as on <c>CreateMemberCommand</c>.</param>
public sealed record SignUpCommand(string Ticket, string FullName, DateOnly? BirthDate);
