using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Application.Attendances;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Letting a guest in the way the desk does (BUSINESS_RULES.md §7 <i>Guest visit</i>): a name and
/// the locker clicked, or none for a reserve place.
/// </summary>
internal static class TestGuests
{
    internal const string GuestCheckInPath = "/api/attendance/guest-check-in";

    internal static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string token, string guestName, int? lockerNumber)
    {
        var lockerId = lockerNumber is { } number ? TestLockers.IdOf(number) : (Guid?)null;
        var request = new HttpRequestMessage(HttpMethod.Post, GuestCheckInPath)
        {
            Content = JsonContent.Create(new { guestName, lockerId }),
        };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    /// <summary>Lets the guest in and reads the visit, asserting it was created.</summary>
    internal static async Task<AttendanceResponse> CheckInOkAsync(
        HttpClient client, string token, string guestName = "مریم احمدی", int? lockerNumber = 1)
    {
        using var response = await CheckInAsync(client, token, guestName, lockerNumber);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }
}
