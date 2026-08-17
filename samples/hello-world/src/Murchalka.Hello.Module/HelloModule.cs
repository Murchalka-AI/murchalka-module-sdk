using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleSdk.Abstractions;

namespace Murchalka.Hello.Module;

/// <summary>Provides the sample greeting capability.</summary>
public sealed class HelloModule(HelloLifecycleState? lifecycleState = null) : IMurchalkaModule, IModuleLifecycle
{
    private readonly HelloLifecycleState _lifecycleState = lifecycleState ?? new HelloLifecycleState();

    /// <inheritdoc/>
    public ModuleId Id { get; } = new("dev.murchalka.hello");
    /// <inheritdoc/>
    public SemanticVersion Version { get; } = SemanticVersion.Parse("1.0.0");
    /// <inheritdoc/>
    public void Configure(IModuleBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.RegisterCapability<GreetHandler, GreetRequest, GreetResponse>(new CapabilityId("hello.greet"),
            SemanticVersion.Parse("1.0.0"), new CapabilityBehavior(CapabilitySideEffect.None,
                CapabilityIdempotency.ReadOnly, TimeSpan.FromSeconds(2), 4096, CancellationRequired: true));
    }
    /// <inheritdoc/>
    public ValueTask OnActivatedAsync(IModuleActivationContext context, CancellationToken cancellationToken)
    { ArgumentNullException.ThrowIfNull(context); cancellationToken.ThrowIfCancellationRequested(); _lifecycleState.MarkActivated(); return ValueTask.CompletedTask; }
    /// <inheritdoc/>
    public ValueTask OnDrainAsync(CancellationToken cancellationToken)
    { _lifecycleState.MarkDraining(); return ValueTask.CompletedTask; }
    /// <inheritdoc/>
    public ValueTask OnStoppedAsync(CancellationToken cancellationToken)
    { _lifecycleState.MarkStopped(); return ValueTask.CompletedTask; }
}
