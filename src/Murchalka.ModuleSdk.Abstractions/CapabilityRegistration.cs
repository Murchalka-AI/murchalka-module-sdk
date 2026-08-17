using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Describes a capability registered by executable module code.</summary>
/// <param name="Id">The capability identifier.</param><param name="Version">The capability version.</param>
/// <param name="HandlerType">The handler type.</param><param name="RequestType">The request type.</param>
/// <param name="ResponseType">The response type.</param><param name="Behavior">The declared behavior.</param>
public sealed record CapabilityRegistration(CapabilityId Id, SemanticVersion Version, Type HandlerType,
    Type RequestType, Type ResponseType, CapabilityBehavior Behavior);
