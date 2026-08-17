namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Defines the executable behavior of a capability.</summary>
/// <param name="SideEffect">The side-effect classification.</param><param name="Idempotency">The idempotency policy.</param>
/// <param name="DefaultTimeout">The default execution timeout.</param><param name="MaximumPayloadBytes">The maximum payload size.</param>
/// <param name="CancellationRequired">Whether handlers must honor cancellation.</param>
public sealed record CapabilityBehavior(CapabilitySideEffect SideEffect, CapabilityIdempotency Idempotency,
    TimeSpan DefaultTimeout, int MaximumPayloadBytes, bool CancellationRequired);
