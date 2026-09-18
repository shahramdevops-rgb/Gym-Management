using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Security;
using Gym.Domain.Auth;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// The <c>refresh_tokens</c> table's own defences, proved below the application code.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RefreshTokenTableTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task RefreshTokens_DuplicateHash_RejectedByDatabase()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        await SaveAsync(RefreshToken.Issue(user.Id, "same-hash", Now));

        var exception = await Should.ThrowAsync<DbUpdateException>(
            () => SaveAsync(RefreshToken.Issue(user.Id, "same-hash", Now)));

        exception.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
    }

    [Fact]
    public async Task RefreshTokens_RevokedAtWithoutReason_RejectedByCheckConstraint()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        var token = RefreshToken.Issue(user.Id, RefreshTokenSecret.Hash("t"), Now);
        await SaveAsync(token);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exception = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE refresh_tokens SET revoked_at = now()",
            TestContext.Current.CancellationToken));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_refresh_tokens_revocation_complete");
    }

    [Fact]
    public async Task RefreshToken_RotatedConcurrently_SecondSaveFailsWithConcurrencyException()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        var original = RefreshToken.Issue(user.Id, RefreshTokenSecret.Hash("original"), Now);
        await SaveAsync(original);

        // Two requests, each with its own context, both read the token while it is active.
        await using var firstScope = Fixture.CreateScope();
        await using var secondScope = Fixture.CreateScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstCopy = await firstDb.RefreshTokens.SingleAsync(TestContext.Current.CancellationToken);
        var secondCopy = await secondDb.RefreshTokens.SingleAsync(TestContext.Current.CancellationToken);

        firstDb.RefreshTokens.Add(firstCopy.Rotate(RefreshTokenSecret.Hash("first-child"), Now).Value);
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        secondDb.RefreshTokens.Add(secondCopy.Rotate(RefreshTokenSecret.Hash("second-child"), Now).Value);

        // xmin changed when the first rotation was saved, so the second is refused instead of
        // giving one parent two children. RefreshHandler turns this into reuse detection.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => secondDb.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private async Task SaveAsync(RefreshToken token)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.RefreshTokens.Add(token);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
