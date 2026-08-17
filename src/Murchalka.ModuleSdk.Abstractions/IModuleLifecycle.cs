namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Defines optional module lifecycle callbacks.</summary>
public interface IModuleLifecycle
{
    /// <summary>Notifies the module that it has been activated.</summary>
    ValueTask OnActivatedAsync(IModuleActivationContext context, CancellationToken cancellationToken);
    /// <summary>Notifies the module to begin graceful draining.</summary>
    ValueTask OnDrainAsync(CancellationToken cancellationToken);
    /// <summary>Notifies the module that it has stopped.</summary>
    ValueTask OnStoppedAsync(CancellationToken cancellationToken);
}
