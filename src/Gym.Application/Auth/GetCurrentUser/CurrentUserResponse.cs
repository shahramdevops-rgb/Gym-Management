namespace Gym.Application.Auth.GetCurrentUser;

/// <summary>Who is logged in, for the frontend's header and for choosing which menus to show.</summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string UserName,
    string FullName,
    IReadOnlyList<string> Roles,
    bool MustChangePassword);
