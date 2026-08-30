using System.Runtime.CompilerServices;
using Murchalka.ModuleSdk.Protocols;

namespace Murchalka.ModuleSdk.Tests;

internal sealed class WaitingProtocolHandler : IProtocolRouteHandler
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async IAsyncEnumerable<ProtocolRouteEvent> HandleAsync(
        ProtocolRouteRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        yield break;
    }
}
