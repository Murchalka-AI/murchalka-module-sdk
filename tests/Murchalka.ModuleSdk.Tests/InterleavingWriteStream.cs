namespace Murchalka.ModuleSdk.Tests;

internal sealed class InterleavingWriteStream : MemoryStream
{
    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        const int maximumChunkSize = 2;
        for (var offset = 0; offset < buffer.Length; offset += maximumChunkSize)
        {
            var count = Math.Min(maximumChunkSize, buffer.Length - offset);
            await base.WriteAsync(buffer.Slice(offset, count), cancellationToken).ConfigureAwait(false);
            await Task.Yield();
        }
    }
}
