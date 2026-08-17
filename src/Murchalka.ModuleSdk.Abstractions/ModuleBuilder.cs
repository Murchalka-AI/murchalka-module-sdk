using Murchalka.ModuleProtocol.Contracts;

namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Collects and validates executable capability registrations.</summary>
public sealed class ModuleBuilder : IModuleBuilder
{
    private readonly Dictionary<(CapabilityId Id, int Major), CapabilityRegistration> _registrations = [];

    /// <summary>Gets the registered capabilities.</summary>
    public IReadOnlyCollection<CapabilityRegistration> Registrations => _registrations.Values;

    /// <inheritdoc/>
    public void RegisterCapability<THandler, TRequest, TResponse>(CapabilityId id, SemanticVersion version, CapabilityBehavior behavior)
        where THandler : class, ICapabilityHandler<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(behavior);
        if (behavior.DefaultTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(behavior));
        if (behavior.MaximumPayloadBytes is < 1 or > ProtocolConstants.MaximumJsonFrameBytes) throw new ArgumentOutOfRangeException(nameof(behavior));
        if (behavior.SideEffect == CapabilitySideEffect.NonIdempotent && behavior.Idempotency != CapabilityIdempotency.Required)
            throw new ArgumentException("Non-idempotent capabilities must require an idempotency key.", nameof(behavior));
        var key = (id, version.Major);
        if (!_registrations.TryAdd(key, new CapabilityRegistration(id, version, typeof(THandler), typeof(TRequest), typeof(TResponse), behavior)))
            throw new InvalidOperationException($"Capability '{id}' major {version.Major} is already registered.");
    }
}
