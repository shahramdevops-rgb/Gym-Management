namespace Gym.Application.SiteRecords.GetMyDetails;

/// <summary>
/// The member's details as the desk entered them, read-only on the site (BUSINESS_RULES.md §15
/// <i>The member's own records</i>). The desk's «یادداشت» is left out: the staff write it for
/// themselves.
/// </summary>
/// <param name="JoinedAt">When the desk registered them, the profile's «تاریخ ثبت».</param>
/// <param name="SignedUpAt">When their site account was made, at their first sign-in.</param>
public sealed record MyDetailsResponse(
    string FullName,
    string PhoneNumber,
    DateOnly BirthDate,
    DateTimeOffset JoinedAt,
    DateTimeOffset SignedUpAt);
