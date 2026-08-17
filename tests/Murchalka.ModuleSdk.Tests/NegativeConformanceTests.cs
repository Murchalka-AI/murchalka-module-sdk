using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleSdk.Abstractions;

namespace Murchalka.ModuleSdk.Tests;

public sealed class NegativeConformanceTests
{
    [Fact]
    public void Duplicate_capability_major_is_rejected()
    {
        var builder = new ModuleBuilder();
        var behavior = new CapabilityBehavior(CapabilitySideEffect.None, CapabilityIdempotency.ReadOnly, TimeSpan.FromSeconds(1), 1024, true);
        builder.RegisterCapability<NoopHandler, string, string>(new CapabilityId("sample.read"), SemanticVersion.Parse("1.0.0"), behavior);

        Assert.Throws<InvalidOperationException>(() =>
            builder.RegisterCapability<NoopHandler, string, string>(new CapabilityId("sample.read"), SemanticVersion.Parse("1.1.0"), behavior));
    }

    [Fact]
    public void Non_idempotent_side_effect_requires_key_semantics()
    {
        var builder = new ModuleBuilder();
        var behavior = new CapabilityBehavior(CapabilitySideEffect.NonIdempotent, CapabilityIdempotency.Optional, TimeSpan.FromSeconds(1), 1024, true);

        Assert.Throws<ArgumentException>(() =>
            builder.RegisterCapability<NoopHandler, string, string>(new CapabilityId("sample.write"), SemanticVersion.Parse("1.0.0"), behavior));
    }
}
