namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Handles typed capability requests.</summary>
public interface ICapabilityHandler<in TRequest, TResponse>
{
    /// <summary>Handles one capability request.</summary>
    ValueTask<CapabilityOutcome<TResponse>> HandleAsync(CapabilityRequestContext context, TRequest request,
        CancellationToken cancellationToken);
}
