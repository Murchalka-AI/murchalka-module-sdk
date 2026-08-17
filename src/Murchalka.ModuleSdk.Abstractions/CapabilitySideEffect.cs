namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Describes the side-effect behavior of a capability.</summary>
public enum CapabilitySideEffect
{
    /// <summary>The capability has no side effects.</summary>
    None,
    /// <summary>The capability has repeatable side effects.</summary>
    Idempotent,
    /// <summary>The capability has non-repeatable side effects.</summary>
    NonIdempotent
}
