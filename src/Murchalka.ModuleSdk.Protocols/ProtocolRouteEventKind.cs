namespace Murchalka.ModuleSdk.Protocols;

/// <summary>Identifies the semantic kind of a protocol route event.</summary>
public enum ProtocolRouteEventKind
{
    /// <summary>The operation reported progress.</summary>
    Progress,

    /// <summary>The operation produced a stream message.</summary>
    Message,

    /// <summary>The operation completed successfully.</summary>
    Completed,

    /// <summary>The operation failed with a normalized public error.</summary>
    Failed
}
