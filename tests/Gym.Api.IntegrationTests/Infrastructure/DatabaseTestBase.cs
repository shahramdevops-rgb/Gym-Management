namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for tests that touch the database: it empties the tables before each one.
/// </summary>
/// <remarks>
/// Isolation is inherited rather than remembered. A test that starts with rows left behind by
/// whichever test happened to run before it is the classic source of a suite that passes on
/// its own, fails in a full run, and passes again when somebody reruns it.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public abstract class DatabaseTestBase(DatabaseFixture fixture) : IAsyncLifetime
{
    protected DatabaseFixture Fixture { get; } = fixture;

    public async ValueTask InitializeAsync() => await Fixture.ResetAsync();

    public ValueTask DisposeAsync()
    {
        // Nothing to release: the container, the factory and the connection belong to the
        // fixture and outlive every individual test. SuppressFinalize keeps CA1816 satisfied
        // for a derived class that one day introduces a finalizer.
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}
