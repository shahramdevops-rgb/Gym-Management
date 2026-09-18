namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Groups every database-backed test class around one <see cref="DatabaseFixture"/>.
/// </summary>
/// <remarks>
/// <para>
/// A collection fixture rather than an assembly fixture: the tests that need no database —
/// result mapping, the validation filter, the middleware — run in milliseconds, and an
/// assembly fixture would make every one of those runs wait for Docker to start Postgres
/// first. This way the container comes up when the first database test asks for it and is
/// torn down as soon as the collection is finished.
/// </para>
/// <para>
/// xUnit runs the classes inside one collection serially, which here is a requirement rather
/// than a cost: they share a single database, and Respawn truncating tables underneath a test
/// running in parallel would delete the rows that test had just written.
/// </para>
/// </remarks>
// Named ...Definition rather than ...Collection because CA1711 reserves the Collection suffix
// for types that actually are collections. Only the attribute's name matters to xUnit.
[CollectionDefinition(Name)]
public sealed class DatabaseCollectionDefinition : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
