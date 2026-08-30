namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Associates one route namespace with its bounded handler and limits.</summary>
/// <param name="RouteNamespace">The unique lower-case route namespace.</param>
/// <param name="ProviderModule">The authenticated provider module identifier.</param>
/// <param name="Handler">The protocol route handler.</param>
/// <param name="Limits">The hard route limits.</param>
public sealed record ProtocolRouteRegistration(
    string RouteNamespace,
    string ProviderModule,
    IProtocolRouteHandler Handler,
    ProtocolRouteLimits Limits);
