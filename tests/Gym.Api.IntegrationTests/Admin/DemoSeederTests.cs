using Gym.Api.Admin;
using Gym.Api.Admin.Demo;
using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Admin;

/// <summary>
/// <c>admin seed-demo</c> (roadmap Phase 12): a made-up gym written through the real handlers, and
/// the guards that keep it away from real data.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class DemoSeederTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task SeedDemo_EmptyDatabase_FillsThreeMonthsUpToNow()
    {
        var owner = await CreateOwnerAsync();
        await using var host = DemoHost();
        var startedAt = DateTimeOffset.UtcNow;

        var (exitCode, output) = await RunAsync(host);

        exitCode.ShouldBe(0, output);
        output.ShouldStartWith("Seeded 90 days");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;

        (await db.Members.CountAsync(ct)).ShouldBeGreaterThan(40);
        (await db.Attendances.CountAsync(ct)).ShouldBeGreaterThan(500);

        var firstVisit = await db.Attendances.MinAsync(a => a.CheckedInAt, ct);
        firstVisit.ShouldBeLessThan(startedAt.AddDays(-89), "the visits are spread over three months, not stamped today.");
        (await db.Attendances.MaxAsync(a => a.CheckedInAt, ct)).ShouldBeLessThan(startedAt, "nothing is dated in the future.");
        (await db.Attendances.CountAsync(a => a.CheckedOutAt == null, ct))
            .ShouldBeGreaterThan(0, "some members are inside right now.");
        (await db.Attendances.CountAsync(a => a.GuestName != null, ct)).ShouldBeGreaterThan(0);
        (await db.Attendances.CountAsync(a => a.AutoClosedAt != null, ct)).ShouldBeGreaterThan(0, "the night's job closed forgotten visits.");

        (await db.Payments.CountAsync(p => p.Kind == PaymentKind.Refund, ct)).ShouldBe(1, "the plan sold by mistake.");
        (await db.Subscriptions.CountAsync(s => s.CancelledAt != null, ct)).ShouldBe(1);
        (await db.Subscriptions.CountAsync(s => s.FrozenSince != null, ct)).ShouldBe(1);
        (await db.Members.CountAsync(m => !m.IsActive, ct)).ShouldBe(1);
        (await db.CafeOrders.CountAsync(ct)).ShouldBeGreaterThan(0);
        (await db.ServiceCharges.CountAsync(ct)).ShouldBeGreaterThan(0);
        (await db.Expenses.CountAsync(ct)).ShouldBeGreaterThan(10);
        (await db.Payables.CountAsync(p => p.PaidAt != null, ct)).ShouldBeGreaterThan(0);
        (await db.Payables.CountAsync(p => p.PaidAt == null, ct)).ShouldBeGreaterThan(0);
        (await db.Notifications.CountAsync(ct)).ShouldBeGreaterThan(0, "the SMS runs sent through the fake sender.");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var staff = await userManager.GetUsersInRoleAsync(Roles.Staff);
        staff.Count.ShouldBe(2);
        (await db.Attendances.CountAsync(a => staff.Select(s => (Guid?)s.Id).Contains(a.CreatedBy), ct))
            .ShouldBeGreaterThan(0, "the desk's work is signed by the staff.");
        (await db.PriceLists.SingleAsync(ct)).UpdatedBy.ShouldBe(owner.Id, "the Owner set the prices.");
    }

    [Fact]
    public async Task SeedDemo_FrozenMemberHadAVisitPlannedThatEvening_PlanStaysFrozen()
    {
        // Run as if on 2026-10-15, a day the member frozen at 13:00 had a visit planned for later
        // that day: the check-in unfroze the plan, and from then on the full run above failed on
        // some days and passed on others.
        await CreateOwnerAsync();
        var startedAt = new FakeTimeProvider(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero));
        await using var host = Fixture.CreateHost(services =>
        {
            services.AddDemoSeedServices();
            services.RemoveAll<DemoClock>();
            services.AddSingleton(new DemoClock(startedAt));
        });

        var (exitCode, output) = await RunAsync(host);

        exitCode.ShouldBe(0, output);
        output.ShouldStartWith("Seeded 90 days up to 2026-10-15");
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Subscriptions.CountAsync(s => s.FrozenSince != null, TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task SeedDemo_DatabaseWithAMember_IsRefusedAndAddsNothing()
    {
        await CreateOwnerAsync();
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(TestMembers.Seed("عضو واقعی", "+989121234567"));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var host = DemoHost();

        var (exitCode, output) = await RunAsync(host);

        exitCode.ShouldBe(1);
        output.ShouldContain("Demo.DatabaseNotEmpty");
        await using var check = Fixture.CreateScope();
        var after = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await after.Members.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await after.Attendances.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task SeedDemo_NoOwner_IsRefused()
    {
        await using var host = DemoHost();

        var (exitCode, output) = await RunAsync(host);

        exitCode.ShouldBe(1);
        output.ShouldContain("Demo.NoOwner");
    }

    [Fact]
    public async Task SeedDemo_HostWithoutTheDemoServices_Throws()
    {
        await CreateOwnerAsync();

        await Should.ThrowAsync<InvalidOperationException>(() => AdminConsole.RunAsync(
            Fixture.Services, [DemoSeeder.Command], TextReader.Null, TextWriter.Null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Production", false, false)]
    [InlineData("Production", true, true)]
    [InlineData("Development", false, true)]
    [InlineData("Testing", false, true)]
    public void IsAllowedHere_Environment_OnlyProductionNeedsTheSetting(string environment, bool allowed, bool expected)
    {
        var host = new HostingEnvironment { EnvironmentName = environment };

        DemoSeeder.IsAllowedHere(host, allowed).ShouldBe(expected);
    }

    private WebApplicationFactory<Program> DemoHost() => Fixture.CreateHost(services => services.AddDemoSeedServices());

    /// <summary>The Owner the seeding at startup would have made, and the staff role it makes with it.</summary>
    private async Task<User> CreateOwnerAsync()
    {
        var owner = await TestUsers.CreateAsync(Fixture, userName: "boss", role: Roles.Owner);
        await using var scope = Fixture.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(Roles.Staff))
        {
            (await roles.CreateAsync(new IdentityRole<Guid>(Roles.Staff))).Succeeded.ShouldBeTrue();
        }

        return owner;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(WebApplicationFactory<Program> host)
    {
        await using var writer = new StringWriter();

        var exitCode = await AdminConsole.RunAsync(
            host.Services, [DemoSeeder.Command], TextReader.Null, writer, TestContext.Current.CancellationToken);

        return (exitCode, writer.ToString());
    }
}
