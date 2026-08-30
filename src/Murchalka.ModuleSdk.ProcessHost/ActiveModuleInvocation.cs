namespace Murchalka.ModuleSdk.ProcessHost;

internal sealed class ActiveModuleInvocation : IDisposable
{
    private string? _cancellationReason;

    public ActiveModuleInvocation(CancellationTokenSource cancellation) => Cancellation = cancellation;

    public CancellationTokenSource Cancellation { get; }

    public Task Completion { get; set; } = Task.CompletedTask;

    public string? CancellationReason => Volatile.Read(ref _cancellationReason);

    public void Cancel(string reason)
    {
        Interlocked.CompareExchange(ref _cancellationReason, reason, null);
        Cancellation.Cancel();
    }

    public void Dispose() => Cancellation.Dispose();
}
