using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Defines the executable entry point of a Murchalka module.</summary>
public interface IMurchalkaModule
{
    /// <summary>Gets the module identifier.</summary>
    ModuleId Id { get; }
    /// <summary>Gets the module version.</summary>
    SemanticVersion Version { get; }
    /// <summary>Registers the module's executable capabilities.</summary>
    void Configure(IModuleBuilder builder);
}
