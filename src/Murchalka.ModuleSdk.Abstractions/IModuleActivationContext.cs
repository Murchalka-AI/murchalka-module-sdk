using System.Text.Json;
using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Provides the immutable context supplied when a module is activated.</summary>
public interface IModuleActivationContext
{
    /// <summary>Gets the module instance identifier.</summary>
    InstanceId InstanceId { get; }
    /// <summary>Gets the runtime-provided time source.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the effective configuration.</summary>
    JsonElement Configuration { get; }
    /// <summary>Gets the granted dependency bindings.</summary>
    IReadOnlyList<GrantedDependency> Dependencies { get; }
}
