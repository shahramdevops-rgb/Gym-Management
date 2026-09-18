namespace Gym.Application.Members.UpdateMember;

/// <param name="Version">The <c>version</c> from the member as it was read before editing.</param>
public sealed record UpdateMemberCommand(string FullName, string PhoneNumber, string? Notes, uint Version);
