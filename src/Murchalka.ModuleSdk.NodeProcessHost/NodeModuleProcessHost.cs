using System.Text.Json;
using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleProtocol.Json;
using Murchalka.ModuleSdk.Node;

namespace Murchalka.ModuleSdk.NodeProcessHost;

/// <summary>Runs a transport-free Node module over the bounded standard-stream process protocol.</summary>
public static class NodeModuleProcessHost
{
    /// <summary>Configures the module, proves its manifest identity, and serves invocations until stopped.</summary>
    /// <param name="module">The Node module implementation.</param>
    /// <param name="cancellationToken">Stops the process host.</param>
    public static async Task RunAsync(INodeModule module, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        var builder = new NodeModuleBuilder();
        module.Configure(builder);
        var registrations = builder.Build();
        if (registrations.Count == 0) throw new InvalidOperationException("A Node module must register at least one capability.");
        var moduleId = new ModuleId(Required("MURCHALKA_NODE_MODULE_ID"));
        var version = SemanticVersion.Parse(Required("MURCHALKA_NODE_MODULE_VERSION"));
        if (registrations.Any(value => value.Descriptor.ModuleId != moduleId || value.Descriptor.ModuleVersion != version))
            throw new InvalidOperationException("Configured capability identity does not match the verified bundle identity.");
        var hello = new NodeModuleHello(moduleId, version, Required("MURCHALKA_NODE_BUNDLE_DIGEST"),
            Required("MURCHALKA_NODE_ARTIFACT_ID"), 1, registrations.Select(value => value.Descriptor).ToArray());
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        await WriteAsync(output, NodeModuleMessageKind.Hello, Guid.Empty, hello, cancellationToken).ConfigureAwait(false);
        var active = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await LengthPrefixedJson.ReadAsync<NodeModuleFrame>(input, cancellationToken: cancellationToken).ConfigureAwait(false);
            switch (frame.Kind)
            {
                case NodeModuleMessageKind.Activate:
                    active = true;
                    await WriteAsync(output, NodeModuleMessageKind.Health, frame.OperationId, new NodeModuleHealth(true, DateTimeOffset.UtcNow, null), cancellationToken).ConfigureAwait(false);
                    break;
                case NodeModuleMessageKind.Drain:
                    active = false;
                    await WriteAsync(output, NodeModuleMessageKind.Health, frame.OperationId, new NodeModuleHealth(false, DateTimeOffset.UtcNow, "draining"), cancellationToken).ConfigureAwait(false);
                    break;
                case NodeModuleMessageKind.Stop:
                    return;
                case NodeModuleMessageKind.Invoke:
                    var invocation = frame.Payload.Deserialize<NodeModuleInvocation>(ProtocolJson.Options) ?? throw new InvalidDataException("Node module invocation is invalid.");
                    var update = active
                        ? await InvokeAsync(registrations, invocation.Task, cancellationToken).ConfigureAwait(false)
                        : new NodeTaskUpdate(invocation.Task.TaskId, NodeTaskState.Rejected, 1, DateTimeOffset.UtcNow, "module-inactive", true, null);
                    await WriteAsync(output, NodeModuleMessageKind.Result, frame.OperationId, update, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported Node module frame '{frame.Kind}'.");
            }
        }
    }

    private static async Task<NodeTaskUpdate> InvokeAsync(IReadOnlyList<NodeModuleRegistration> registrations, NodeTask task, CancellationToken shutdownToken)
    {
        var registration = registrations.SingleOrDefault(value => value.Descriptor.CapabilityId == task.CapabilityId && value.Descriptor.Version == task.CapabilityVersion);
        if (registration is null) return new NodeTaskUpdate(task.TaskId, NodeTaskState.Rejected, 1, DateTimeOffset.UtcNow, "capability-not-registered", false, null);
        var remaining = task.Deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return new NodeTaskUpdate(task.TaskId, NodeTaskState.Rejected, 1, DateTimeOffset.UtcNow, "deadline-expired", false, null);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        deadline.CancelAfter(remaining);
        try
        {
            var context = new NodeExecutionContext(task.TaskId, task.ActorReference, task.Purpose, task.AllowedPaths,
                task.AllowedNetwork, task.ResourceBudget, task.Deadline, task.IdempotencyKey);
            var payload = await registration.Handler.ExecuteAsync(task.Arguments, context, deadline.Token).ConfigureAwait(false);
            return new NodeTaskUpdate(task.TaskId, NodeTaskState.Succeeded, 2, DateTimeOffset.UtcNow, null, false, payload);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return new NodeTaskUpdate(task.TaskId, NodeTaskState.Cancelled, 2, DateTimeOffset.UtcNow, "cancelled", false, null);
        }
        catch (Exception)
        {
            return new NodeTaskUpdate(task.TaskId, NodeTaskState.Failed, 2, DateTimeOffset.UtcNow, "module-handler-failed", false, null);
        }
    }

    private static ValueTask WriteAsync<T>(Stream output, NodeModuleMessageKind kind, Guid operationId, T payload,
        CancellationToken cancellationToken) => LengthPrefixedJson.WriteAsync(output,
        new NodeModuleFrame(kind, operationId, JsonSerializer.SerializeToElement(payload, ProtocolJson.Options)), cancellationToken: cancellationToken);

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is missing.");
}
