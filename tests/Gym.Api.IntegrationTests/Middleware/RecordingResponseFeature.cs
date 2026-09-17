using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Gym.Api.IntegrationTests.Middleware;

/// <summary>
/// A response feature that actually records <c>OnStarting</c> callbacks.
/// </summary>
/// <remarks>
/// The <c>HttpResponseFeature</c> behind a plain <c>DefaultHttpContext</c> implements
/// <c>OnStarting</c> as a no-op, because nothing is ever written to a socket. A test using it
/// unchanged would pass whether or not the middleware registered anything — it would assert on
/// a header that was never going to be set either way. This records the callbacks so a test
/// can fire them at the moment a real server would: just before the first byte goes out.
/// </remarks>
public sealed class RecordingResponseFeature : IHttpResponseFeature
{
    private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    public string? ReasonPhrase { get; set; }

    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

    public Stream Body { get; set; } = Stream.Null;

    public bool HasStarted { get; private set; }

    public void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));

    public void OnCompleted(Func<object, Task> callback, object state)
    {
        // Not needed by these tests, and a silent no-op would be a trap, so say so.
        throw new NotSupportedException("OnCompleted is not recorded by this feature.");
    }

    /// <summary>Runs the registered callbacks in registration order, as a real server does.</summary>
    public async Task StartResponseAsync()
    {
        HasStarted = true;

        foreach (var (callback, state) in _onStarting)
        {
            await callback(state);
        }
    }
}
