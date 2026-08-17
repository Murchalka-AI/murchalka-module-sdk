using Murchalka.ModuleSdk.Abstractions;

namespace Murchalka.Hello.Module;

/// <summary>Handles sample greeting requests.</summary>
public sealed class GreetHandler : ICapabilityHandler<GreetRequest, GreetResponse>
{
    /// <inheritdoc/>
    public ValueTask<CapabilityOutcome<GreetResponse>> HandleAsync(CapabilityRequestContext context,
        GreetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80)
            return ValueTask.FromResult(CapabilityOutcome<GreetResponse>.Failure(new ModuleError(
                "invalid-name", ModuleErrorCategory.InvalidRequest, Retryable: false,
                "Name must contain between 1 and 80 characters.")));
        return ValueTask.FromResult(CapabilityOutcome<GreetResponse>.Success(new GreetResponse($"Hello, {request.Name}!")));
    }
}
