using System.Text.Json;
using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleSdk.Abstractions;

namespace Murchalka.ModuleSdk.Tests;

internal sealed class ActivationContext : IModuleActivationContext
{
    public InstanceId InstanceId { get; } = new("default");
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public JsonElement Configuration { get; } = JsonSerializer.SerializeToElement(new { });
    public IReadOnlyList<GrantedDependency> Dependencies { get; } = [];
}
