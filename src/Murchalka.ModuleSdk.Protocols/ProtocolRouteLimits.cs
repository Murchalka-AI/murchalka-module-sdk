namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Defines hard limits applied to an external protocol route.</summary>
/// <param name="MaximumPayloadBytes">The maximum request payload size.</param>
/// <param name="MaximumConcurrency">The maximum concurrent requests.</param>
/// <param name="Timeout">The maximum request duration.</param>
public sealed record ProtocolRouteLimits(int MaximumPayloadBytes, int MaximumConcurrency, TimeSpan Timeout)
{
    /// <summary>Validates the declared limits.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A limit is outside the supported range.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumPayloadBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumPayloadBytes, 4 * 1024 * 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumConcurrency, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumConcurrency, 4096);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Timeout, TimeSpan.FromMinutes(30));
    }
}
