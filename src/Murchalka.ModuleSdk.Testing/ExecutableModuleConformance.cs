using System.Text.Json.Nodes;
using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleProtocol.Json;
using Murchalka.ModuleSdk.Abstractions;

namespace Murchalka.ModuleSdk.Testing;

/// <summary>Validates executable module code against its verified repository declarations.</summary>
public sealed class ExecutableModuleConformance
{
    /// <summary>Validates a module implementation and its repository artifacts.</summary>
    public ConformanceReport Validate(string moduleRoot, IMurchalkaModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var findings = new List<ConformanceFinding>();
        var repositoryReport = new ModuleRepositoryConformance().Validate(moduleRoot);
        findings.AddRange(repositoryReport.Findings);
        if (!repositoryReport.Passed) return new ConformanceReport(findings);

        var manifest = StructuredDocument.Load(Path.Combine(moduleRoot, "murchalka.module.yaml")).AsObject();
        var manifestId = manifest["metadata"]!["id"]!.GetValue<string>();
        var manifestVersion = manifest["metadata"]!["version"]!.GetValue<string>();
        if (!string.Equals(module.Id.Value, manifestId, StringComparison.Ordinal))
            findings.Add(Error("ManifestConformance", "module-id-mismatch", "metadata.id", $"Executable id '{module.Id}' does not match '{manifestId}'."));
        if (!string.Equals(module.Version.ToString(), manifestVersion, StringComparison.Ordinal))
            findings.Add(Error("ManifestConformance", "module-version-mismatch", "metadata.version", $"Executable version '{module.Version}' does not match '{manifestVersion}'."));

        var builder = new ModuleBuilder();
        try { module.Configure(builder); }
        catch (Exception exception)
        {
            findings.Add(Error("LifecycleConformance", "configure-failed", module.Id.Value, exception.Message));
            return new ConformanceReport(findings);
        }

        var declared = manifest["provides"]?["capabilities"]?.AsArray()
            .Select(node => node!.AsObject())
            .ToDictionary(
                capability => (capability["id"]!.GetValue<string>(), SemanticVersion.Parse(capability["version"]!.GetValue<string>()).Major),
                capability => capability) ?? [];

        foreach (var registration in builder.Registrations)
        {
            var key = (registration.Id.Value, registration.Version.Major);
            if (!declared.TryGetValue(key, out var capability))
            {
                findings.Add(Error("CapabilityContractConformance", "undeclared-capability", registration.Id.Value, "Executable registered a capability major absent from the verified manifest."));
                continue;
            }

            if (!string.Equals(registration.Version.ToString(), capability["version"]!.GetValue<string>(), StringComparison.Ordinal))
                findings.Add(Error("CapabilityContractConformance", "registration-version-mismatch", registration.Id.Value, "Executable and manifest capability versions differ."));

            var execution = capability["execution"]!.AsObject();
            CompareEnum(registration.Behavior.Idempotency.ToString(), execution["idempotency"]!.GetValue<string>(), "idempotency", registration.Id.Value, findings);
            var expectedTimeout = ParseDuration(execution["timeout"]!.GetValue<string>());
            if (registration.Behavior.DefaultTimeout != expectedTimeout)
                findings.Add(Error("CapabilityContractConformance", "timeout-mismatch", registration.Id.Value, "Executable and manifest timeouts differ."));
        }

        foreach (var declaration in declared)
            if (!builder.Registrations.Any(registration => registration.Id.Value == declaration.Key.Item1 && registration.Version.Major == declaration.Key.Item2))
                findings.Add(Error("CapabilityContractConformance", "declared-capability-not-registered", declaration.Key.Item1, "Manifest capability was not registered by the executable."));

        return new ConformanceReport(findings);
    }

    /// <summary>Determines whether a handler honors a token cancelled before invocation.</summary>
    public static async ValueTask<bool> HonorsPreCancelledTokenAsync<TRequest, TResponse>(
        ICapabilityHandler<TRequest, TResponse> handler,
        CapabilityRequestContext context,
        TRequest request)
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        try
        {
            _ = await handler.HandleAsync(context, request, source.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            return true;
        }
    }

    /// <summary>Determines whether repeated handler execution produces an equal outcome.</summary>
    public static async ValueTask<bool> IsReplayStableAsync<TRequest, TResponse>(
        ICapabilityHandler<TRequest, TResponse> handler,
        CapabilityRequestContext context,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var first = await handler.HandleAsync(context, request, cancellationToken).ConfigureAwait(false);
        var second = await handler.HandleAsync(context, request, cancellationToken).ConfigureAwait(false);
        return EqualityComparer<CapabilityOutcome<TResponse>>.Default.Equals(first, second);
    }

    private static void CompareEnum(string actual, string expected, string field, string location, ICollection<ConformanceFinding> findings)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            findings.Add(Error("IdempotencyConformance", $"{field}-mismatch", location, $"Executable '{actual}' does not match manifest '{expected}'."));
    }

    private static TimeSpan ParseDuration(string duration)
    {
        var unitLength = duration.EndsWith("ms", StringComparison.Ordinal) ? 2 : 1;
        var value = long.Parse(duration[..^unitLength], System.Globalization.CultureInfo.InvariantCulture);
        return duration[^unitLength..] switch
        {
            "ms" => TimeSpan.FromMilliseconds(value),
            "s" => TimeSpan.FromSeconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            "d" => TimeSpan.FromDays(value),
            _ => throw new FormatException($"Unknown duration '{duration}'.")
        };
    }

    private static ConformanceFinding Error(string check, string code, string location, string message) =>
        new(check, code, ConformanceSeverity.Error, location, message);
}
