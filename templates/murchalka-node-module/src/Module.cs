using Murchalka.ModuleSdk.Node;

namespace Company.Product.NodeFeature;

/// <summary>Configures the Node feature.</summary>
public sealed class Module : INodeModule
{
    /// <inheritdoc/>
    public void Configure(INodeModuleBuilder builder) => ArgumentNullException.ThrowIfNull(builder);
}
