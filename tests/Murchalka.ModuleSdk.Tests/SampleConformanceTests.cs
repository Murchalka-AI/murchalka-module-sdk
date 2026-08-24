using Murchalka.Hello.Module;
using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleSdk.Abstractions;
using Murchalka.ModuleSdk.Testing;

namespace Murchalka.ModuleSdk.Tests;

public sealed class SampleConformanceTests
{
    [Fact]
    public void Sample_repository_passes_schema_and_semantic_conformance()
    {
        var report = new ModuleRepositoryConformance().Validate(SampleRoot);

        Assert.True(report.Passed, Format(report));
    }

    [Fact]
    public void Sample_executable_matches_verified_manifest()
    {
        var report = new ExecutableModuleConformance().Validate(SampleRoot, new HelloModule());

        Assert.True(report.Passed, Format(report));
    }

    [Fact]
    public async Task Lifecycle_has_no_effect_before_activate_and_drains_deterministically()
    {
        var state = new HelloLifecycleState();
        var module = new HelloModule(state);
        module.Configure(new ModuleBuilder());
        Assert.False(state.Activated);

        await module.OnActivatedAsync(new ActivationContext(), TestContext.Current.CancellationToken);
        await module.OnDrainAsync(TestContext.Current.CancellationToken);
        await module.OnStoppedAsync(TestContext.Current.CancellationToken);

        Assert.True(state.Activated);
        Assert.True(state.Draining);
        Assert.True(state.Stopped);
    }

    [Fact]
    public async Task Read_only_handler_honors_cancellation_and_is_replay_stable()
    {
        var handler = new GreetHandler();
        var context = RequestContext();

        Assert.True(await ExecutableModuleConformance.HonorsPreCancelledTokenAsync(handler, context, new GreetRequest("Murchalka")));
        Assert.True(await ExecutableModuleConformance.IsReplayStableAsync(
            handler,
            context,
            new GreetRequest("Murchalka"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Invalid_input_returns_normalized_non_retryable_error()
    {
        var outcome = await new GreetHandler().HandleAsync(RequestContext(), new GreetRequest(""), TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.Equal("invalid-name", outcome.Error!.Code);
        Assert.False(outcome.Error.Retryable);
    }

    private static string SampleRoot => Path.Combine(AppContext.BaseDirectory, "sample");

    private static CapabilityRequestContext RequestContext() => new(
        Guid.Parse("078ff6a9-cbab-42f3-8c6f-55ec18b5883a"),
        new ModuleId("dev.murchalka.consumer"),
        "greeting",
        new DateTimeOffset(2026, 8, 17, 10, 0, 2, TimeSpan.Zero),
        null,
        "person:owner",
        new Dictionary<string, string> { ["tenant"] = "home" },
        "correlation-01");

    private static string Format(ConformanceReport report) => string.Join(
        Environment.NewLine,
        report.Findings.Select(finding => $"{finding.Check}/{finding.Code}: {finding.Message}"));
}
