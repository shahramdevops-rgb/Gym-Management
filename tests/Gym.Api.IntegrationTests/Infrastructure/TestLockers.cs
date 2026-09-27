using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Application.Attendances;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Infrastructure.Persistence.Seed;

using Npgsql;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The gym's seeded lockers as tests need them: by number, and checking a member in the way the
/// desk does since roadmap 6.5.5 — by choosing a locker (BUSINESS_RULES.md §7).
/// </summary>
internal static class TestLockers
{
    /// <summary>The seeded id of locker <paramref name="number"/>, 1 to 72.</summary>
    internal static Guid IdOf(int number) => LockerSeed.IdOf(number);

    /// <summary>
    /// Checks a member in with locker <paramref name="lockerNumber"/>, or, when none is named, with
    /// the lowest-numbered free locker in service — the one the desk would see first on the map. A
    /// test that is not about lockers then needs to know nothing about them.
    /// </summary>
    internal static async Task<HttpResponseMessage> CheckInAsync(
        HttpClient client, string token, Guid memberId, int? lockerNumber = null)
    {
        var lockerId = lockerNumber is { } number ? IdOf(number) : await FirstFreeIdAsync(client, token);

        return await CheckInWithAsync(client, token, memberId, lockerId);
    }

    /// <summary>Asks for a reserve place: a check-in with no locker (BUSINESS_RULES.md §6).</summary>
    internal static Task<HttpResponseMessage> CheckInOnReservePlaceAsync(HttpClient client, string token, Guid memberId) =>
        CheckInWithAsync(client, token, memberId, lockerId: null);

    /// <summary>Checks in and reads the visit, asserting it was created.</summary>
    internal static async Task<AttendanceResponse> CheckInOkAsync(
        HttpClient client, string token, Guid memberId, int? lockerNumber = null)
    {
        using var response = await CheckInAsync(client, token, memberId, lockerNumber);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    internal static Task<HttpResponseMessage> CheckInWithAsync(HttpClient client, string token, Guid memberId, Guid? lockerId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in")
        {
            Content = JsonContent.Create(new { lockerId }),
        };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Takes every locker out of service but the ones named, straight in the database. With none
    /// free, a reserve place becomes usable (BUSINESS_RULES.md §6), and a test gets there without
    /// seventy-two check-ins.
    /// </summary>
    internal static async Task TakeOutOfServiceAllButAsync(DatabaseFixture fixture, params int[] keep)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE lockers SET is_out_of_service = NOT (number = ANY(@keep))", connection);
        command.Parameters.AddWithValue("keep", keep);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> FirstFreeIdAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/lockers?pageSize={PagingRules.MaxPageSize}");
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<LockerResponse>>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();

        return page.Items.First(locker => !locker.IsOutOfService && !locker.IsOccupied).Id;
    }
}
