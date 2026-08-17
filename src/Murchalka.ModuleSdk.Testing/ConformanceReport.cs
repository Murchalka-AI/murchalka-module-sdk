namespace Murchalka.ModuleSdk.Testing;

/// <summary>Contains the findings produced by module conformance checks.</summary>
/// <param name="Findings">The collected findings.</param>
public sealed record ConformanceReport(IReadOnlyList<ConformanceFinding> Findings)
{
    /// <summary>Gets whether no error-level findings were produced.</summary>
    public bool Passed => Findings.All(finding => finding.Severity != ConformanceSeverity.Error);
    /// <summary>Gets the number of error-level findings.</summary>
    public int ErrorCount => Findings.Count(finding => finding.Severity == ConformanceSeverity.Error);
}
