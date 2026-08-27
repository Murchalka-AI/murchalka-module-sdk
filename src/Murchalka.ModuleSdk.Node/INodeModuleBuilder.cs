using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Node;

/// <summary>Registers transport-free Node capability handlers.</summary>
public interface INodeModuleBuilder
{
    /// <summary>Registers one declared capability provider.</summary>
    void AddCapability(NodeCapabilityDescriptor descriptor, INodeCapabilityHandler handler);
}
