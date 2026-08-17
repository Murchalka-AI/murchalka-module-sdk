using Murchalka.ModuleSdk.Abstractions;

namespace Murchalka.ModuleSdk.Tests;

internal sealed class NoopHandler : ICapabilityHandler<string, string>
{
    public ValueTask<CapabilityOutcome<string>> HandleAsync(CapabilityRequestContext context, string request,
        CancellationToken cancellationToken) => ValueTask.FromResult(CapabilityOutcome<string>.Success(request));
}
