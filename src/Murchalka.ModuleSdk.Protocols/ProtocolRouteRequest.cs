using System.Text.Json;

namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Contains one bounded request accepted by the generic protocol gateway.</summary>
/// <param name="RequestId">The unique request identifier.</param>
/// <param name="RouteNamespace">The resolved route namespace.</param>
/// <param name="Method">The normalized external method.</param>
/// <param name="Path">The namespace-relative path.</param>
/// <param name="Headers">The allowlisted, non-secret request headers.</param>
/// <param name="Payload">The untrusted request payload.</param>
/// <param name="Peer">The authenticated peer identity.</param>
/// <param name="CorrelationId">The distributed correlation identifier.</param>
/// <param name="Deadline">The request deadline.</param>
public sealed record ProtocolRouteRequest(
    string RequestId,
    string RouteNamespace,
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    JsonElement Payload,
    ProtocolPeerIdentity Peer,
    string CorrelationId,
    DateTimeOffset Deadline);
