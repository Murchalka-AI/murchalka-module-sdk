namespace Murchalka.ModuleSdk.Testing;

/// <summary>Describes one module conformance finding.</summary>
/// <param name="Check">The conformance check.</param><param name="Code">The machine-readable code.</param>
/// <param name="Severity">The severity.</param><param name="Location">The source location.</param>
/// <param name="Message">The human-readable message.</param>
public sealed record ConformanceFinding(string Check, string Code, ConformanceSeverity Severity,
    string Location, string Message);
