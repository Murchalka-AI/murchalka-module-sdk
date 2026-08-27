using System.Text.Json;

namespace Murchalka.ModuleSdk.Node;

/// <summary>Executes one Node capability after Runtime grant and local-policy validation.</summary>
public interface INodeCapabilityHandler
{
    /// <summary>Executes the bounded request.</summary>
    ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, NodeExecutionContext context, CancellationToken cancellationToken);
}
