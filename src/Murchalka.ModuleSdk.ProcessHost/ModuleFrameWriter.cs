namespace Murchalka.ModuleSdk.ProcessHost;

internal sealed class ModuleFrameWriter : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ModuleFrameWriter(Stream stream) => _stream = stream;

    public async ValueTask WriteAsync<T>(string kind, T payload, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await GatewayFrameCodec.WriteAsync(_stream, kind, payload, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
