using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Registers executable capabilities exposed by a module.</summary>
public interface IModuleBuilder
{
    /// <summary>Registers a typed capability handler.</summary>
    void RegisterCapability<THandler, TRequest, TResponse>(CapabilityId id, SemanticVersion version, CapabilityBehavior behavior)
        where THandler : class, ICapabilityHandler<TRequest, TResponse>;
}
