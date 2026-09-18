using Gym.Api.IntegrationTests.Common;
using Gym.Infrastructure.Persistence.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Persistence;

/// <summary>
/// The audit stamp is the one rule no use case is allowed to implement itself, so it gets
/// tested on its own. The interceptor is driven directly against a change tracker: no
/// database is involved, and no clock either, because FakeTimeProvider pins "now".
/// </summary>
public sealed class AuditableEntityInterceptorTests
{
    private static readonly DateTimeOffset Created = new(2026, 3, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 3, 22, 17, 30, 0, TimeSpan.Zero);

    private static readonly Guid Alice = Guid.CreateVersion7();
    private static readonly Guid Bob = Guid.CreateVersion7();

    private readonly FakeTimeProvider _time = new(Created);
    private readonly FakeCurrentUser _currentUser = new() { UserId = Alice };
    private readonly AuditableEntityInterceptor _interceptor;

    public AuditableEntityInterceptorTests() => _interceptor = new AuditableEntityInterceptor(_time, _currentUser);

    [Fact]
    public async Task SavingChanges_WhenEntityAdded_SetsCreatedByToTheCurrentUser()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = new TestEntity();
        context.Add(entity);

        await SaveAsync(context);

        entity.CreatedBy.ShouldBe(Alice);
        entity.UpdatedBy.ShouldBeNull();
    }

    [Fact]
    public async Task SavingChanges_WhenEntityModified_SetsUpdatedByAndKeepsCreatedBy()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = await AddAndSaveAsync(context);

        _currentUser.UserId = Bob;
        entity.DisplayName = "changed";
        await SaveAsync(context);

        entity.CreatedBy.ShouldBe(Alice, "who created the row never changes.");
        entity.UpdatedBy.ShouldBe(Bob);
    }

    [Fact]
    public async Task SavingChanges_WithNoCurrentUser_LeavesCreatedByNull()
    {
        _currentUser.UserId = null;
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = new TestEntity();
        context.Add(entity);

        await SaveAsync(context);

        // Seeding, background jobs and anonymous requests act on nobody's behalf.
        entity.CreatedBy.ShouldBeNull();
    }

    [Fact]
    public async Task SavingChanges_WhenEntityAdded_SetsCreatedAtFromTheTimeProvider()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = new TestEntity();
        context.Add(entity);

        await SaveAsync(context);

        entity.CreatedAt.ShouldBe(Created);
    }

    [Fact]
    public async Task SavingChanges_WhenEntityAdded_StampsAUtcOffset()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = new TestEntity();
        context.Add(entity);

        await SaveAsync(context);

        // Npgsql throws on a timestamptz whose offset is not zero, so a clock that ever
        // handed back a local time would fail at the database. Better to fail here.
        entity.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task SavingChanges_WhenEntityAdded_LeavesUpdatedAtNull()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = new TestEntity();
        context.Add(entity);

        await SaveAsync(context);

        // An insert is not an update. A row that has never changed must be distinguishable
        // from one that has, which is exactly what a null UpdatedAt means.
        entity.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task SavingChanges_WhenEntityModified_SetsUpdatedAtAndKeepsCreatedAt()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = await AddAndSaveAsync(context);

        _time.SetUtcNow(Updated);
        entity.DisplayName = "changed";

        await SaveAsync(context);

        entity.UpdatedAt.ShouldBe(Updated);

        // The failure this guards against: an interceptor that stamps CreatedAt on every
        // save looks correct in a one-save test and silently destroys history in production.
        entity.CreatedAt.ShouldBe(Created);
    }

    [Fact]
    public async Task SavingChanges_WhenCalledTwiceOnAnUnchangedEntity_StampsNothing()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = await AddAndSaveAsync(context);

        _time.SetUtcNow(Updated);

        // Nothing was touched between the two saves, so the entry is Unchanged.
        await SaveAsync(context);

        entity.CreatedAt.ShouldBe(Created);
        entity.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task SavingChanges_WhenEntityDeleted_StampsNothing()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var entity = await AddAndSaveAsync(context);

        _time.SetUtcNow(Updated);
        context.Remove(entity);

        await SaveAsync(context);

        // The row is going away; task 1.6's audit log records the deletion itself.
        entity.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task SavingChanges_WhenSeveralEntitiesAdded_StampsThemAllWithTheSameInstant()
    {
        using var context = PersistenceTestContext.Create(_interceptor);
        var first = new TestEntity();
        var second = new TestEntity();
        context.AddRange(first, second);

        await SaveAsync(context);

        // One timestamp per save, not one per row: everything written by the same
        // transaction should agree on when it happened.
        first.CreatedAt.ShouldBe(second.CreatedAt);
    }

    [Fact]
    public async Task SavingChanges_WhenContextIsNull_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => SaveAsync(context: null));
    }

    private async Task<TestEntity> AddAndSaveAsync(PersistenceTestContext context)
    {
        var entity = new TestEntity();
        context.Add(entity);

        await SaveAsync(context);

        // What a real SaveChanges does after the write: the entry settles into Unchanged, so
        // the next save sees a Modified entry only if something is actually touched.
        context.Entry(entity).State = EntityState.Unchanged;

        return entity;
    }

    /// <summary>
    /// Invokes the interceptor through its real entry point without going to a database.
    /// The event definition and message generator are null because EF only reads them to
    /// build a log message, which is not what is under test.
    /// </summary>
    private Task<InterceptionResult<int>> SaveAsync(DbContext? context) =>
        _interceptor
            .SavingChangesAsync(
                new DbContextEventData(null!, null!, context),
                default,
                TestContext.Current.CancellationToken)
            .AsTask();
}
