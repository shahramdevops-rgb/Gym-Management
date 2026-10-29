using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Trainers;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Trainers;

/// <summary>
/// <c>/api/trainers</c>, «مربیان» (BUSINESS_RULES.md §15 <i>Trainers</i>): the Owner makes someone
/// a trainer, corrects them, stops them and makes them one again. Owner only, reading included.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TrainerEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TrainersPath = "/api/trainers";
    private const string Phone = "+989121234567";
    private const string OtherPhone = "+989351234567";

    // ---- Who may ----

    [Fact]
    public async Task ListTrainers_AsStaff_Returns403()
    {
        var (client, _, staff) = await ClientsAsync();

        using var response = await SendAsync(client, staff, HttpMethod.Get, TrainersPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTrainer_AsStaff_Returns403AndMakesNothing()
    {
        var (client, owner, staff) = await ClientsAsync();

        using var response = await CreateAsync(client, staff, "علی کریمی", Phone);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ListAsync(client, owner)).ShouldBeEmpty();
    }

    // ---- Create ----

    [Fact]
    public async Task CreateTrainer_AsOwner_Returns201AndIsATrainerFromToday()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "علی کریمی", "0912 123 4567");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var trainer = await ReadAsync(response);
        trainer.FullName.ShouldBe("علی کریمی");
        trainer.PhoneNumber.ShouldBe(Phone, "the phone is normalized as a member's is.");
        trainer.IsActive.ShouldBeTrue();
        trainer.LastSignedInAt.ShouldBeNull();
        var period = trainer.Periods.ShouldHaveSingleItem();
        period.StartedOn.ShouldBe(await TodayAsync());
        period.EndedOn.ShouldBeNull();
        response.Headers.Location.ShouldNotBeNull().ToString().ShouldBe($"{TrainersPath}/{trainer.Id}");
    }

    [Fact]
    public async Task CreateTrainer_PhoneOfAMember_Succeeds()
    {
        // §15: a trainer is not a member, but may also be one, with the same phone.
        await SeedMemberAsync(Phone);
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "علی کریمی", Phone);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTrainer_PhoneOfAStoppedTrainer_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        var first = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);
        (await StopAsync(client, owner, first.Id)).EnsureSuccessStatusCode();

        using var response = await CreateAsync(client, owner, "رضا احمدی", "09121234567");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Trainers.PhoneAlreadyExists");
    }

    [Fact]
    public async Task CreateTrainer_LandlinePhone_Returns400()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "علی کریمی", "02188776655");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.PhoneNotMobile");
    }

    [Fact]
    public async Task CreateTrainer_BlankName_Returns400()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "  ", Phone);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("fullName")[0].GetProperty("code").GetString().ShouldBe("Trainers.FullNameRequired");
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateTrainer_NewNameAndPhone_SavesBoth()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);

        using var response = await UpdateAsync(client, owner, trainer.Id, "علی کریمی‌نژاد", OtherPhone, trainer.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync(response);
        updated.FullName.ShouldBe("علی کریمی‌نژاد");
        updated.PhoneNumber.ShouldBe(OtherPhone);
        updated.Version.ShouldNotBe(trainer.Version);
    }

    [Fact]
    public async Task UpdateTrainer_StaleVersion_Returns409AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);
        (await UpdateAsync(client, owner, trainer.Id, "علی کریمی‌نژاد", Phone, trainer.Version)).EnsureSuccessStatusCode();

        using var response = await UpdateAsync(client, owner, trainer.Id, "رضا", OtherPhone, trainer.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Trainers.ChangedConcurrently");
        (await ListAsync(client, owner)).ShouldHaveSingleItem().FullName.ShouldBe("علی کریمی‌نژاد");
    }

    [Fact]
    public async Task UpdateTrainer_AnotherTrainersPhone_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreateTrainerAsync(client, owner, "علی کریمی", Phone);
        var other = await CreateTrainerAsync(client, owner, "رضا احمدی", OtherPhone);

        using var response = await UpdateAsync(client, owner, other.Id, "رضا احمدی", Phone, other.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Trainers.PhoneAlreadyExists");
    }

    [Fact]
    public async Task UpdateTrainer_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await UpdateAsync(client, owner, Guid.CreateVersion7(), "علی کریمی", Phone, version: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Trainers.NotFound");
    }

    // ---- Stop and restart ----

    [Fact]
    public async Task StopTrainer_ActiveTrainer_EndsTheirPeriodToday()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);

        using var response = await StopAsync(client, owner, trainer.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stopped = await ReadAsync(response);
        stopped.IsActive.ShouldBeFalse();
        stopped.Periods.ShouldHaveSingleItem().EndedOn.ShouldBe(await TodayAsync());
    }

    [Fact]
    public async Task StopTrainer_AlreadyStopped_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);
        (await StopAsync(client, owner, trainer.Id)).EnsureSuccessStatusCode();

        using var response = await StopAsync(client, owner, trainer.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Trainers.AlreadyStopped");
    }

    [Fact]
    public async Task RestartTrainer_StoppedTrainer_OpensASecondPeriodAndKeepsTheFirst()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);
        (await StopAsync(client, owner, trainer.Id)).EnsureSuccessStatusCode();

        using var response = await SendAsync(client, owner, HttpMethod.Post, $"{TrainersPath}/{trainer.Id}/restart", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var restarted = await ReadAsync(response);
        restarted.IsActive.ShouldBeTrue();
        restarted.Periods.Count.ShouldBe(2);
        restarted.Periods[0].EndedOn.ShouldNotBeNull();
        restarted.Periods[1].EndedOn.ShouldBeNull();
    }

    [Fact]
    public async Task RestartTrainer_ActiveTrainer_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);

        using var response = await SendAsync(client, owner, HttpMethod.Post, $"{TrainersPath}/{trainer.Id}/restart", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Trainers.AlreadyActive");
    }

    [Fact]
    public async Task DeleteTrainer_AsOwner_HasNoEndpoint()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);

        using var response = await SendAsync(client, owner, HttpMethod.Delete, $"{TrainersPath}/{trainer.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    // ---- List ----

    [Fact]
    public async Task ListTrainers_StoppedAndActive_TrainersNowComeFirst()
    {
        var (client, owner, _) = await ClientsAsync();
        var stopped = await CreateTrainerAsync(client, owner, "الف قدیمی", Phone);
        (await StopAsync(client, owner, stopped.Id)).EnsureSuccessStatusCode();
        await CreateTrainerAsync(client, owner, "ی جدید", OtherPhone);

        var trainers = await ListAsync(client, owner);

        trainers.Select(trainer => trainer.FullName).ShouldBe(["ی جدید", "الف قدیمی"]);
    }

    // ---- The database's own guards ----

    [Fact]
    public async Task Database_SecondOpenPeriodForOneTrainer_IsRefused()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"INSERT INTO trainer_periods (id, trainer_id, started_on, created_at) VALUES ('{Guid.CreateVersion7()}', '{trainer.Id}', DATE '2026-11-01', now())"));

        exception.ConstraintName.ShouldBe(TrainerConstraints.OneOpenPeriod);
    }

    [Fact]
    public async Task Database_PeriodEndingBeforeItStarts_IsRefused()
    {
        var (client, owner, _) = await ClientsAsync();
        var trainer = await CreateTrainerAsync(client, owner, "علی کریمی", Phone);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE trainer_periods SET ended_on = started_on - 1 WHERE trainer_id = '{trainer.Id}'"));

        exception.ConstraintName.ShouldBe("ck_trainer_periods_ended_on_not_before_started_on");
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Owner, string Staff)> ClientsAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client,
            await client.LoginForAccessTokenAsync("owner", TestUsers.Password),
            await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<DateOnly> TodayAsync()
    {
        await using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<Gym.Application.Common.IGymCalendar>().Today();
    }

    private async Task SeedMemberAsync(string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(TestMembers.Seed("علی کریمی", phone));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
            .ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
    }

    private static async Task<TrainerResponse> CreateTrainerAsync(HttpClient client, string token, string fullName, string phone)
    {
        using var response = await CreateAsync(client, token, fullName, phone);
        response.EnsureSuccessStatusCode();

        return await ReadAsync(response);
    }

    private static async Task<List<TrainerResponse>> ListAsync(HttpClient client, string token)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, TrainersPath, body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<TrainerResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<TrainerResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<TrainerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string token, string fullName, string phoneNumber) =>
        SendAsync(client, token, HttpMethod.Post, TrainersPath, new { fullName, phoneNumber });

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, Guid id, string fullName, string phoneNumber, uint version) =>
            SendAsync(client, token, HttpMethod.Put, $"{TrainersPath}/{id}", new { fullName, phoneNumber, version });

    private static Task<HttpResponseMessage> StopAsync(HttpClient client, string token, Guid id) =>
        SendAsync(client, token, HttpMethod.Post, $"{TrainersPath}/{id}/stop", body: null);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
