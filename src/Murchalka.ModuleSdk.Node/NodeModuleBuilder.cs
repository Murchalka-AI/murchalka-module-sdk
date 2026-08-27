using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Node;

/// <summary>Collects deterministic Node capability registrations.</summary>
public sealed class NodeModuleBuilder : INodeModuleBuilder
{
    private readonly Dictionary<InstanceId, NodeModuleRegistration> _registrations = [];

    /// <inheritdoc/>
    public void AddCapability(NodeCapabilityDescriptor descriptor, INodeCapabilityHandler handler) { ArgumentNullException.ThrowIfNull(descriptor); ArgumentNullException.ThrowIfNull(handler); if (!_registrations.TryAdd(descriptor.ProviderInstance, new NodeModuleRegistration(descriptor, handler))) throw new InvalidOperationException("Provider instance is already registered."); }

    /// <summary>Returns the stable registration set.</summary>
    public IReadOnlyList<NodeModuleRegistration> Build() => _registrations.Values.OrderBy(value => value.Descriptor.ProviderInstance.Value, StringComparer.Ordinal).ToArray();
}
