namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Maps an external protocol request to bounded route events.</summary>
public interface IProtocolRouteHandler
{
    /// <summary>Handles one authenticated and bounded protocol request.</summary>
    /// <param name="request">The untrusted request and trusted peer context.</param>
    /// <param name="cancellationToken">A token cancelled by deadline, disconnect, disable, or revocation.</param>
    /// <returns>The ordered route event stream.</returns>
    IAsyncEnumerable<ProtocolRouteEvent> HandleAsync(ProtocolRouteRequest request, CancellationToken cancellationToken);
}
