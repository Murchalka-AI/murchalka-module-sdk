using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleProtocol.Json;

namespace Murchalka.ModuleSdk.ProcessHost;

/// <summary>Runs a module service over the authenticated Runtime process protocol.</summary>
public static class ModuleProcessHost
{
    /// <summary>Connects to Runtime and serves requests until shutdown.</summary>
    public static async Task RunAsync(IModuleService service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        var socketPath = Required("MURCHALKA_SOCKET");
        var moduleId = new ModuleId(Required("MURCHALKA_MODULE_ID"));
        var moduleVersion = SemanticVersion.Parse(Required("MURCHALKA_MODULE_VERSION"));
        var bundleDigest = Required("MURCHALKA_BUNDLE_DIGEST");
        var artifactId = Required("MURCHALKA_ARTIFACT_ID");
        var instanceId = new InstanceId(Required("MURCHALKA_INSTANCE_ID"));
        var capabilitiesDigest = Required("MURCHALKA_CAPABILITIES_DIGEST");
        var proofKey = Convert.FromBase64String(Required("MURCHALKA_PROOF_KEY"));

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken).ConfigureAwait(false);
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        var hello = new ModuleHello(
            moduleId, moduleVersion, bundleDigest, instanceId, [1], artifactId, ModuleTarget.Runtime,
            Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            capabilitiesDigest, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
        await GatewayFrameCodec.WriteAsync(stream, "moduleHello", hello, cancellationToken).ConfigureAwait(false);
        var challenge = GatewayFrameCodec.PayloadAs<RuntimeChallenge>(await GatewayFrameCodec.ReadAsync(stream, cancellationToken).ConfigureAwait(false));
        ValidateChallenge(hello, challenge);
        var transcript = string.Join('\n', "murchalka-module-proof-v1", hello.ModuleId.Value,
            hello.ModuleVersion.ToString(), hello.BundleDigest, hello.InstanceId.Value, hello.ArtifactId,
            hello.DeclaredCapabilitiesDigest, challenge.SelectedProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            challenge.ModuleNonce, challenge.RuntimeNonce);
        ModuleProof proof;
        try
        {
            proof = new ModuleProof(moduleId, instanceId, challenge.RuntimeNonce, challenge.ModuleNonce,
                Convert.ToBase64String(HMACSHA256.HashData(proofKey, Encoding.UTF8.GetBytes(transcript))));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(proofKey);
        }
        await GatewayFrameCodec.WriteAsync(stream, "moduleProof", proof, cancellationToken).ConfigureAwait(false);
        var configuration = GatewayFrameCodec.PayloadAs<ConfigurationSnapshot>(await GatewayFrameCodec.ReadAsync(stream, cancellationToken).ConfigureAwait(false));
        _ = await GatewayFrameCodec.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        var dependencies = GatewayFrameCodec.PayloadAs<DependencyEndpointsSnapshot>(await GatewayFrameCodec.ReadAsync(stream, cancellationToken).ConfigureAwait(false));
        await GatewayFrameCodec.WriteAsync(stream, "moduleReady", new ModuleReady(moduleId, instanceId, capabilitiesDigest, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
        await using var writer = new ModuleFrameWriter(stream);
        await ProcessAsync(service, new ModuleDependencyClient(writer, moduleId, dependencies), writer, stream, configuration, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ProcessAsync(
        IModuleService service,
        ModuleDependencyClient dependencies,
        ModuleFrameWriter writer,
        Stream stream,
        ConfigurationSnapshot initialConfiguration,
        CancellationToken cancellationToken)
    {
        var configuration = initialConfiguration;
        var invocations = new ConcurrentDictionary<Guid, ActiveModuleInvocation>();
        var active = false;
        var running = true;
        try
        {
            while (running)
            {
                var frame = await GatewayFrameCodec.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
                if (string.Equals(frame.Kind, "capabilityResult", StringComparison.Ordinal))
                {
                    dependencies.Complete(GatewayFrameCodec.PayloadAs<ResultEnvelope>(frame));
                    continue;
                }
                if (string.Equals(frame.Kind, "invocationCancellation", StringComparison.Ordinal))
                {
                    var cancellation = GatewayFrameCodec.PayloadAs<JsonElement>(frame);
                    var invocationId = cancellation.GetProperty("invocationId").GetGuid();
                    var reason = cancellation.GetProperty("reason").GetString() ?? "caller-cancelled";
                    if (invocations.TryGetValue(invocationId, out var activeInvocation))
                        activeInvocation.Cancel(reason);
                    continue;
                }
                if (string.Equals(frame.Kind, "control", StringComparison.Ordinal))
                {
                    var control = GatewayFrameCodec.PayloadAs<ControlMessage>(frame);
                    if (control.Kind == ControlMessageKind.HealthProbe)
                    {
                        await writer.WriteAsync("health", new ModuleHealth(
                            active ? ModuleHealthStatus.Ready : ModuleHealthStatus.NotReady,
                            DateTimeOffset.UtcNow, active ? [] : ["inactive"]), cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    if (control.Deadline <= DateTimeOffset.UtcNow)
                    {
                        await writer.WriteAsync("controlResult", new ControlResult(
                            control.OperationId, false, "deadline-exceeded", "Control deadline elapsed.", null), cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    switch (control.Kind)
                    {
                        case ControlMessageKind.Activate: active = true; break;
                        case ControlMessageKind.Drain:
                            active = false;
                            CancelAll(invocations, "module-draining");
                            break;
                        case ControlMessageKind.Stop:
                            active = false;
                            running = false;
                            CancelAll(invocations, "module-stopping");
                            break;
                        case ControlMessageKind.ReloadConfiguration:
                            configuration = control.Payload.Deserialize<ConfigurationSnapshot>(ProtocolJson.Options)
                                ?? throw new InvalidDataException("Configuration snapshot is invalid.");
                            break;
                        case ControlMessageKind.UpdateBindings:
                            dependencies.Update(control.Payload.Deserialize<DependencyEndpointsSnapshot>(ProtocolJson.Options)
                                ?? throw new InvalidDataException("Dependency snapshot is invalid."));
                            break;
                    }
                    await writer.WriteAsync("controlResult", new ControlResult(
                        control.OperationId, true, null, null, null), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (!string.Equals(frame.Kind, "invocation", StringComparison.Ordinal))
                    throw new InvalidDataException($"Unexpected protocol frame '{frame.Kind}'.");
                var invocation = GatewayFrameCodec.PayloadAs<InvocationEnvelope>(frame);
                var remaining = invocation.Deadline - DateTimeOffset.UtcNow;
                var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (remaining <= TimeSpan.Zero) lifetime.Cancel(); else lifetime.CancelAfter(remaining);
                var tracked = new ActiveModuleInvocation(lifetime);
                if (!invocations.TryAdd(invocation.InvocationId, tracked))
                {
                    tracked.Dispose();
                    await writer.WriteAsync("result", Failure(invocation.InvocationId, "invocation-id-collision", ErrorCategory.InvalidRequest, false, "Invocation identifier is already active."), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                tracked.Completion = CompleteInvocationAsync(service, dependencies, writer, invocation, configuration.Values, active, tracked, invocations, cancellationToken);
            }
        }
        finally
        {
            CancelAll(invocations, "module-stopping");
            dependencies.FailAll(new OperationCanceledException("Module process host stopped."));
            await Task.WhenAll(invocations.Values.Select(value => value.Completion)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            foreach (var invocation in invocations.Values) invocation.Dispose();
        }
    }

    private static async Task CompleteInvocationAsync(
        IModuleService service,
        IModuleDependencyClient dependencies,
        ModuleFrameWriter writer,
        InvocationEnvelope invocation,
        JsonElement configuration,
        bool active,
        ActiveModuleInvocation tracked,
        ConcurrentDictionary<Guid, ActiveModuleInvocation> invocations,
        CancellationToken shutdownToken)
    {
        try
        {
            var result = await InvokeAsync(service, dependencies, invocation, configuration, active, tracked, shutdownToken).ConfigureAwait(false);
            await writer.WriteAsync("result", result, shutdownToken).ConfigureAwait(false);
        }
        finally
        {
            invocations.TryRemove(invocation.InvocationId, out _);
            tracked.Dispose();
        }
    }

    private static async Task<ResultEnvelope> InvokeAsync(
        IModuleService service,
        IModuleDependencyClient dependencies,
        InvocationEnvelope invocation,
        JsonElement configuration,
        bool active,
        ActiveModuleInvocation tracked,
        CancellationToken shutdownToken)
    {
        if (!active) return Failure(invocation.InvocationId, "module-inactive", ErrorCategory.Unavailable, true, "Module is not active.");
        if (invocation.Payload is not { } payload) return Failure(invocation.InvocationId, "request-invalid", ErrorCategory.InvalidRequest, false, "Invocation payload is required.");
        if (invocation.Deadline <= DateTimeOffset.UtcNow) return Failure(invocation.InvocationId, "deadline-exceeded", ErrorCategory.Timeout, true, "Invocation deadline elapsed.");
        var context = new ModuleInvocationContext(invocation.CapabilityId.Value, invocation.ConsumerModuleId.Value,
            invocation.ActorReference, invocation.Scope, invocation.Purpose, invocation.CorrelationId,
            invocation.Deadline, invocation.IdempotencyKey, configuration);
        try
        {
            var response = await service.HandleAsync(context, payload, dependencies, tracked.Cancellation.Token).ConfigureAwait(false);
            return new ResultEnvelope(invocation.InvocationId, InvocationStatus.Succeeded, response, null, null, [], [], invocation.IdempotencyKey);
        }
        catch (ModuleOperationException exception)
        {
            return Failure(invocation.InvocationId, exception.Code, exception.Category, exception.Retryable, exception.Message);
        }
        catch (OperationCanceledException) when (tracked.Cancellation.IsCancellationRequested && !shutdownToken.IsCancellationRequested)
        {
            return string.Equals(tracked.CancellationReason, "deadline-exceeded", StringComparison.Ordinal) ||
                (tracked.CancellationReason is null && invocation.Deadline <= DateTimeOffset.UtcNow)
                ? new ResultEnvelope(invocation.InvocationId, InvocationStatus.DeadlineExceeded, null,
                    new ProtocolError("deadline-exceeded", ErrorCategory.Timeout, true, "Invocation deadline elapsed.", null), null, [], [], null)
                : new ResultEnvelope(invocation.InvocationId, InvocationStatus.Cancelled, null,
                    new ProtocolError("invocation-cancelled", ErrorCategory.Cancelled, false, "Invocation was cancelled by its caller.", null), null, [], [], null);
        }
        catch (Exception)
        {
            return Failure(invocation.InvocationId, "module-failed", ErrorCategory.Internal, false, "The module could not complete the request.");
        }
    }

    private static ResultEnvelope Failure(Guid invocationId, string code, ErrorCategory category, bool retryable, string message) =>
        new(invocationId, InvocationStatus.Failed, null, new ProtocolError(code, category, retryable, message, null), null, [], [], null);

    private static void CancelAll(ConcurrentDictionary<Guid, ActiveModuleInvocation> invocations, string reason)
    {
        foreach (var invocation in invocations.Values) invocation.Cancel(reason);
    }

    private static void ValidateChallenge(ModuleHello hello, RuntimeChallenge challenge)
    {
        if (challenge.ModuleNonce != hello.Nonce || challenge.ExpiresAt <= DateTimeOffset.UtcNow || challenge.SelectedProtocolVersion != 1)
            throw new InvalidDataException("Runtime challenge is invalid.");
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is missing.");
}
