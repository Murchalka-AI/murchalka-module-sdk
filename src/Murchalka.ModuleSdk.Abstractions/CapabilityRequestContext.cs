using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Provides the security and tracing context for a capability request.</summary>
/// <param name="InvocationId">The invocation identifier.</param><param name="ConsumerModule">The consumer module.</param>
/// <param name="Purpose">The declared purpose.</param><param name="Deadline">The request deadline.</param>
/// <param name="IdempotencyKey">The idempotency key.</param><param name="ActorReference">The actor reference.</param>
/// <param name="Scope">The invocation scope.</param><param name="CorrelationId">The correlation identifier.</param>
public sealed record CapabilityRequestContext(Guid InvocationId, ModuleId ConsumerModule, string Purpose,
    DateTimeOffset Deadline, string? IdempotencyKey, string? ActorReference,
    IReadOnlyDictionary<string, string> Scope, string CorrelationId);
