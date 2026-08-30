using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Provides hot route registration, bounded dispatch, backpressure, and cancellation.</summary>
public sealed partial class ProtocolRouteDispatcher : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ProtocolRouteState> _routes = new(StringComparer.Ordinal);
    private int _disposed;

    /// <summary>Registers a route until the returned lease is disposed.</summary>
    /// <param name="registration">The validated route registration.</param>
    /// <returns>A lease controlling route lifetime.</returns>
    /// <exception cref="InvalidOperationException">The namespace is already registered.</exception>
    public ProtocolRouteLease Register(ProtocolRouteRegistration registration)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(registration);
        if (!RouteNamespacePattern().IsMatch(registration.RouteNamespace))
            throw new ArgumentException("Route namespace must be a bounded lower-case identifier.", nameof(registration));
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.ProviderModule);
        ArgumentNullException.ThrowIfNull(registration.Handler);
        registration.Limits.Validate();
        var state = new ProtocolRouteState(registration);
        if (!_routes.TryAdd(registration.RouteNamespace, state))
        {
            state.Dispose();
            throw new InvalidOperationException($"Protocol route namespace '{registration.RouteNamespace}' is already registered.");
        }

        return new ProtocolRouteLease(this, registration.RouteNamespace);
    }

    /// <summary>Dispatches one bounded request and yields ordered protocol events.</summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">A token cancelled when the peer disconnects.</param>
    /// <returns>The ordered event stream.</returns>
    /// <exception cref="KeyNotFoundException">The route is not active.</exception>
    public async IAsyncEnumerable<ProtocolRouteEvent> DispatchAsync(
        ProtocolRouteRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(request);
        if (!_routes.TryGetValue(request.RouteNamespace, out var route))
            throw new KeyNotFoundException($"Protocol route '{request.RouteNamespace}' is not active.");
        var payloadLength = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request.Payload).Length;
        if (payloadLength > route.Registration.Limits.MaximumPayloadBytes)
            throw new InvalidDataException("Protocol request payload exceeds the declared route limit.");
        var now = DateTimeOffset.UtcNow;
        var effectiveDeadline = request.Deadline < now.Add(route.Registration.Limits.Timeout)
            ? request.Deadline
            : now.Add(route.Registration.Limits.Timeout);
        if (effectiveDeadline <= now) throw new TimeoutException("Protocol request deadline has elapsed.");

        using var requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, route.Lifetime.Token);
        requestLifetime.CancelAfter(effectiveDeadline - now);
        await route.Concurrency.WaitAsync(requestLifetime.Token).ConfigureAwait(false);
        try
        {
            long expectedSequence = 0;
            await foreach (var item in route.Registration.Handler.HandleAsync(request, requestLifetime.Token).WithCancellation(requestLifetime.Token).ConfigureAwait(false))
            {
                if (item.Sequence != expectedSequence++)
                    throw new InvalidDataException("Protocol route event sequence is invalid.");
                yield return item;
            }
        }
        finally
        {
            route.Concurrency.Release();
        }
    }

    internal void Unregister(string routeNamespace)
    {
        if (_routes.TryRemove(routeNamespace, out var state)) state.Dispose();
    }

    /// <summary>Removes every route and cancels all active requests.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
        foreach (var route in _routes.Keys) Unregister(route);
        return ValueTask.CompletedTask;
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex RouteNamespacePattern();
}
