namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Invokes a capability through a granted dependency binding.</summary>
public interface IGrantedCapabilityInvoker
{
    /// <summary>Invokes a typed granted capability.</summary>
    ValueTask<CapabilityOutcome<TResponse>> InvokeAsync<TRequest, TResponse>(string requirementId,
        CapabilityRequestContext context, TRequest request, CancellationToken cancellationToken);
}
