using System.Text.Json;

namespace Murchalka.ModuleSdk.ProcessHost;

/// <summary>Invokes only capabilities present in the Runtime-issued dependency snapshot.</summary>
public interface IModuleDependencyClient
{
    /// <summary>Gets the current non-secret dependency identities granted by Runtime.</summary>
    IReadOnlyList<Murchalka.ModuleSdk.Abstractions.GrantedDependency> GrantedDependencies { get; }

    /// <summary>Invokes a granted dependency by requirement identifier.</summary>
    /// <param name="requirementId">The manifest requirement identifier.</param>
    /// <param name="context">The current trusted invocation context.</param>
    /// <param name="payload">The dependency request payload.</param>
    /// <param name="payloadSchema">The request schema identifier.</param>
    /// <param name="idempotencyKey">The dependency idempotency key, when required.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The successful dependency response payload.</returns>
    ValueTask<JsonElement> InvokeAsync(
        string requirementId,
        ModuleInvocationContext context,
        JsonElement payload,
        string payloadSchema,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Invokes one explicitly selected provider from a multi-provider requirement.</summary>
    /// <param name="requirementId">The manifest requirement identifier.</param>
    /// <param name="providerModule">The granted provider module identifier.</param>
    /// <param name="capability">The granted capability identifier.</param>
    /// <param name="context">The current trusted invocation context.</param>
    /// <param name="payload">The dependency request payload.</param>
    /// <param name="payloadSchema">The request schema identifier.</param>
    /// <param name="idempotencyKey">The dependency idempotency key, when required.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The successful dependency response payload.</returns>
    ValueTask<JsonElement> InvokeSelectedAsync(
        string requirementId,
        string providerModule,
        string capability,
        ModuleInvocationContext context,
        JsonElement payload,
        string payloadSchema,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}
