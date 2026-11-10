namespace Gym.Application.SiteUsers.FindSiteUserByPhone;

/// <summary>A wrapper, so "nobody" is an ordinary <c>200</c> with <c>null</c> inside rather than a 404 the form would have to treat as normal.</summary>
/// <param name="SiteUser">The site user with that exact number, or <c>null</c> when there is none.</param>
public sealed record SiteUserByPhoneResponse(SiteUserResponse? SiteUser);
