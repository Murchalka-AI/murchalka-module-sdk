namespace Murchalka.ModuleSdk.Protocols;

internal sealed class ProtocolRouteState : IDisposable
{
    public ProtocolRouteState(ProtocolRouteRegistration registration)
    {
        Registration = registration;
        Concurrency = new SemaphoreSlim(registration.Limits.MaximumConcurrency, registration.Limits.MaximumConcurrency);
    }

    public ProtocolRouteRegistration Registration { get; }

    public SemaphoreSlim Concurrency { get; }

    public CancellationTokenSource Lifetime { get; } = new();

    public void Dispose()
    {
        Lifetime.Cancel();
        Lifetime.Dispose();
    }
}
