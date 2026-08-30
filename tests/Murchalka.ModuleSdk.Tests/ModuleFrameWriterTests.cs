using Murchalka.ModuleProtocol.Json;
using Murchalka.ModuleSdk.ProcessHost;

namespace Murchalka.ModuleSdk.Tests;

/// <summary>Verifies that concurrent process-host messages preserve frame boundaries.</summary>
public sealed class ModuleFrameWriterTests
{
    /// <summary>Ensures concurrent writes remain independently readable length-prefixed frames.</summary>
    [Fact]
    public async Task ConcurrentWritesRemainFrameAlignedAsync()
    {
        await using var stream = new InterleavingWriteStream();
        await using var writer = new ModuleFrameWriter(stream);
        var writes = Enumerable.Range(0, 64)
            .Select(index => writer.WriteAsync("test", new { index }, TestContext.Current.CancellationToken).AsTask());

        await Task.WhenAll(writes);

        stream.Position = 0;
        var observed = new List<int>();
        for (var index = 0; index < 64; index++)
        {
            var frame = await LengthPrefixedJson.ReadAsync<GatewayFrame>(stream, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("test", frame.Kind);
            observed.Add(frame.Payload.GetProperty("index").GetInt32());
        }

        Assert.Equal(Enumerable.Range(0, 64), observed.Order());
    }
}
