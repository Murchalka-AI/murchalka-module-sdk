namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Classifies module errors.</summary>
public enum ModuleErrorCategory
{
    /// <summary>The request is invalid.</summary>
    InvalidRequest,
    /// <summary>The requested resource was not found.</summary>
    NotFound,
    /// <summary>The request conflicts with current state.</summary>
    Conflict,
    /// <summary>The caller lacks permission.</summary>
    PermissionDenied,
    /// <summary>The service is unavailable.</summary>
    Unavailable,
    /// <summary>The operation timed out.</summary>
    Timeout,
    /// <summary>The operation was cancelled.</summary>
    Cancelled,
    /// <summary>An internal error occurred.</summary>
    Internal
}
