namespace Murchalka.ModuleSdk.Node;

/// <summary>Configures a product Node module without depending on Node Runtime transport.</summary>
public interface INodeModule
{
    /// <summary>Registers the module's manifest-declared Node capabilities.</summary>
    void Configure(INodeModuleBuilder builder);
}
