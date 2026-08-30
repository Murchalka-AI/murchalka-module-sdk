namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Owns one live route registration and removes it on disposal.</summary>
public sealed class ProtocolRouteLease : IAsyncDisposable
{
    private readonly ProtocolRouteDispatcher _dispatcher;
    private readonly string _routeNamespace;
    private int _disposed;

    internal ProtocolRouteLease(ProtocolRouteDispatcher dispatcher, string routeNamespace)
    {
        _dispatcher = dispatcher;
        _routeNamespace = routeNamespace;
    }

    /// <summary>Immediately removes the route and cancels its active requests.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _dispatcher.Unregister(_routeNamespace);
        return ValueTask.CompletedTask;
    }
}
