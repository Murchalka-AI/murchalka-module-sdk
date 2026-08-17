namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Describes the idempotency-key policy of a capability.</summary>
public enum CapabilityIdempotency
{
    /// <summary>The capability is read-only.</summary>
    ReadOnly,
    /// <summary>An idempotency key is optional.</summary>
    Optional,
    /// <summary>An idempotency key is required.</summary>
    Required,
    /// <summary>Idempotent replay is unsupported.</summary>
    Unsupported
}
