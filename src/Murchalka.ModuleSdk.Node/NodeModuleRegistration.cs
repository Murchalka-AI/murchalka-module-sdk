using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Node;

/// <summary>Binds one Node capability descriptor to its handler.</summary>
/// <param name="Descriptor">The capability descriptor.</param><param name="Handler">The capability handler.</param>
public sealed record NodeModuleRegistration(NodeCapabilityDescriptor Descriptor, INodeCapabilityHandler Handler);
