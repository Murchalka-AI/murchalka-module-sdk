using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Describes a dependency granted to a module instance.</summary>
/// <param name="RequirementId">The requirement identifier.</param><param name="ProviderModule">The provider module.</param>
/// <param name="Capability">The capability identifier.</param><param name="Version">The capability version.</param>
/// <param name="Instance">The provider instance.</param>
public sealed record GrantedDependency(string RequirementId, ModuleId ProviderModule, CapabilityId Capability,
    SemanticVersion Version, InstanceId Instance);
