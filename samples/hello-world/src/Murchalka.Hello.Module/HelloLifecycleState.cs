namespace Murchalka.Hello.Module;

/// <summary>Records lifecycle callbacks observed by the sample module.</summary>
public sealed class HelloLifecycleState
{
    /// <summary>Gets whether activation was observed.</summary>
    public bool Activated { get; private set; }
    /// <summary>Gets whether draining was observed.</summary>
    public bool Draining { get; private set; }
    /// <summary>Gets whether stopping was observed.</summary>
    public bool Stopped { get; private set; }
    internal void MarkActivated() => Activated = true;
    internal void MarkDraining() => Draining = true;
    internal void MarkStopped() => Stopped = true;
}
