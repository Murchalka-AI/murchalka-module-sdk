using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Node;

/// <summary>Contains the intersected security and resource scope for one Node invocation.</summary>
/// <param name="TaskId">The task identifier.</param><param name="ActorReference">The actor.</param><param name="Purpose">The purpose.</param><param name="AllowedPaths">Allowed paths.</param><param name="AllowedNetwork">Allowed hosts.</param><param name="ResourceBudget">The resource budget.</param><param name="Deadline">The deadline.</param><param name="IdempotencyKey">The idempotency key.</param>
public sealed record NodeExecutionContext(Guid TaskId, string ActorReference, string Purpose, IReadOnlyList<string> AllowedPaths, IReadOnlyList<string> AllowedNetwork, NodeResourceBudget ResourceBudget, DateTimeOffset Deadline, string IdempotencyKey);
