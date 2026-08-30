using System.Text.Json;
using System.Collections.Concurrent;
using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleSdk.Abstractions;

namespace Murchalka.ModuleSdk.ProcessHost;

internal sealed class ModuleDependencyClient : IModuleDependencyClient
{
    private readonly ModuleFrameWriter _writer;
    private readonly ModuleId _moduleId;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ResultEnvelope>> _pending = new();
    private DependencyEndpointsSnapshot _snapshot;

    public ModuleDependencyClient(ModuleFrameWriter writer, ModuleId moduleId, DependencyEndpointsSnapshot snapshot)
    {
        _writer = writer;
        _moduleId = moduleId;
        _snapshot = snapshot;
    }

    public IReadOnlyList<GrantedDependency> GrantedDependencies => Volatile.Read(ref _snapshot).Endpoints
        .Select(value => new GrantedDependency(value.RequirementId, value.ProviderModule, value.Capability, value.CapabilityVersion, value.ProviderInstance))
        .ToArray();

    public void Update(DependencyEndpointsSnapshot snapshot) =>
        Volatile.Write(ref _snapshot, snapshot ?? throw new ArgumentNullException(nameof(snapshot)));

    public void Complete(ResultEnvelope result)
    {
        if (_pending.TryRemove(result.InvocationId, out var completion)) completion.TrySetResult(result);
    }

    public void FailAll(Exception exception)
    {
        foreach (var pair in _pending.ToArray())
            if (_pending.TryRemove(pair.Key, out var completion)) completion.TrySetException(exception);
    }

    public async ValueTask<JsonElement> InvokeAsync(
        string requirementId,
        ModuleInvocationContext context,
        JsonElement payload,
        string payloadSchema,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var endpoint = Volatile.Read(ref _snapshot).Endpoints.SingleOrDefault(value =>
            string.Equals(value.RequirementId, requirementId, StringComparison.Ordinal))
            ?? throw new ModuleOperationException(
                "dependency-not-granted",
                ErrorCategory.Unavailable,
                $"Required dependency '{requirementId}' is not granted.",
                retryable: true);
        return await InvokeEndpointAsync(endpoint, context, payload, payloadSchema, idempotencyKey, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<JsonElement> InvokeSelectedAsync(
        string requirementId,
        string providerModule,
        string capability,
        ModuleInvocationContext context,
        JsonElement payload,
        string payloadSchema,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var providerId = new ModuleId(providerModule);
        var capabilityId = new CapabilityId(capability);
        var endpoint = Volatile.Read(ref _snapshot).Endpoints.SingleOrDefault(value =>
            string.Equals(value.RequirementId, requirementId, StringComparison.Ordinal) &&
            value.ProviderModule == providerId &&
            value.Capability == capabilityId)
            ?? throw new ModuleOperationException(
                "dependency-not-granted",
                ErrorCategory.PermissionDenied,
                $"Selected dependency '{providerModule}/{capability}' is not granted for requirement '{requirementId}'.",
                retryable: false);
        return await InvokeEndpointAsync(endpoint, context, payload, payloadSchema, idempotencyKey, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<JsonElement> InvokeEndpointAsync(
        DependencyEndpoint endpoint,
        ModuleInvocationContext context,
        JsonElement payload,
        string payloadSchema,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var invocation = new InvocationEnvelope(
            Guid.NewGuid(), endpoint.Capability, endpoint.CapabilityVersion, endpoint.ProviderInstance,
            _moduleId, context.ActorReference, context.Scope, context.Purpose, endpoint.AuthorizationReference,
            context.CorrelationId, context.CorrelationId, null, context.Deadline, idempotencyKey,
            payloadSchema, payload, null);
        var completion = new TaskCompletionSource<ResultEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(invocation.InvocationId, completion)) throw new InvalidOperationException("Dependency invocation identifier collision.");
        try
        {
            await _writer.WriteAsync("capabilityInvocation", invocation, cancellationToken).ConfigureAwait(false);
            var result = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (result.InvocationId != invocation.InvocationId) throw new InvalidDataException("Dependency result invocation id does not match.");
            if (result.Status == InvocationStatus.Succeeded && result.Payload is { } response) return response;
            var error = result.Error;
            throw new ModuleOperationException(
                error?.Code ?? "dependency-failed",
                error?.Category ?? ErrorCategory.Unavailable,
                error?.Message ?? "A granted dependency invocation failed.",
                error?.Retryable ?? true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await SendCancellationAsync(invocation, context, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _pending.TryRemove(invocation.InvocationId, out _);
        }
    }

    private async ValueTask SendCancellationAsync(
        InvocationEnvelope invocation,
        ModuleInvocationContext context,
        CancellationToken cancellationToken)
    {
        var reason = context.Deadline <= DateTimeOffset.UtcNow ? "deadline-exceeded" : "caller-cancelled";
        try
        {
            await _writer.WriteAsync("capabilityCancellation", new { invocationId = invocation.InvocationId, reason }, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }
}
