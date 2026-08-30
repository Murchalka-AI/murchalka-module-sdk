using System.Text.Json;
using Murchalka.ModuleSdk.Protocols;

namespace Murchalka.ModuleSdk.Tests;

/// <summary>Verifies generic protocol route lifetime and collision behavior.</summary>
public sealed class ProtocolRouteDispatcherTests
{
    /// <summary>Verifies that route removal is immediate and cancels in-flight work.</summary>
    [Fact]
    public async Task DisposingLeaseRemovesRouteAndCancelsActiveRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dispatcher = new ProtocolRouteDispatcher();
        var handler = new WaitingProtocolHandler();
        await using var lease = dispatcher.Register(new ProtocolRouteRegistration(
            "mcp", "dev.murchalka.protocol-mcp", handler, new ProtocolRouteLimits(4096, 1, TimeSpan.FromMinutes(1))));
        var request = new ProtocolRouteRequest(
            "request-1", "mcp", "POST", "/", new Dictionary<string, string>(), JsonSerializer.SerializeToElement(new { }),
            new ProtocolPeerIdentity("peer:test", "bearer", "credential:test"), "correlation-1", DateTimeOffset.UtcNow.AddMinutes(1));
        var operation = ConsumeAsync(dispatcher.DispatchAsync(request, cancellationToken), cancellationToken);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        await lease.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            ConsumeAsync(dispatcher.DispatchAsync(request, cancellationToken), cancellationToken));
    }

    /// <summary>Verifies deterministic rejection of ambiguous route ownership.</summary>
    [Fact]
    public async Task DuplicateRouteNamespaceIsRejected()
    {
        await using var dispatcher = new ProtocolRouteDispatcher();
        var registration = new ProtocolRouteRegistration(
            "a2a", "dev.murchalka.protocol-a2a", new WaitingProtocolHandler(), new ProtocolRouteLimits(4096, 1, TimeSpan.FromSeconds(5)));
        await using var lease = dispatcher.Register(registration);

        Assert.Throws<InvalidOperationException>(() => dispatcher.Register(registration));
    }

    private static async Task ConsumeAsync(
        IAsyncEnumerable<ProtocolRouteEvent> events,
        CancellationToken cancellationToken)
    {
        await foreach (var _ in events.WithCancellation(cancellationToken).ConfigureAwait(false)) { }
    }

}
