using Gym.Domain.Common;

namespace Gym.Domain.Tests.Common;

/// <summary>
/// Guards the two promises the base entity makes: ids are version 7 GUIDs, and the audit
/// fields are nobody's business but the interceptor's.
/// </summary>
public sealed class EntityTests
{
    private sealed class TestEntity : Entity;

    [Fact]
    public void Id_WhenEntityCreated_IsVersion7Guid()
    {
        var entity = new TestEntity();

        // The version lives in the high nibble of byte 6 of the RFC 9562 layout. Asserting on
        // it, rather than merely on "not empty", is what makes a silent switch back to
        // Guid.NewGuid() fail here instead of quietly fragmenting the primary key index.
        entity.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Id_WhenEntityCreated_IsNotEmpty()
    {
        var entity = new TestEntity();

        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Id_WhenTwoEntitiesCreated_AreUnique()
    {
        var first = new TestEntity();
        var second = new TestEntity();

        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public async Task Id_WhenEntitiesCreatedInOrder_SortAscending()
    {
        var earlier = new TestEntity();

        // Version 7 orders by a millisecond timestamp, so the ids of two entities created
        // inside the same millisecond may tie. The delay makes the assertion about the
        // property that matters — ordering across time — rather than about tie-breaking.
        await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.Current.CancellationToken);

        var later = new TestEntity();

        // This ordering is the whole reason for choosing version 7: sequential ids mean
        // inserts append to the end of the index instead of scattering across it.
        earlier.Id.CompareTo(later.Id).ShouldBeLessThan(0);
    }

    [Fact]
    public void AuditFields_WhenEntityCreated_AreUnset()
    {
        var entity = new TestEntity();

        // An entity must not stamp itself. If it did, a broken or unregistered interceptor
        // would still leave plausible-looking timestamps and nobody would notice.
        entity.CreatedAt.ShouldBe(default);
        entity.CreatedBy.ShouldBeNull();
        entity.UpdatedAt.ShouldBeNull();
        entity.UpdatedBy.ShouldBeNull();
    }
}
