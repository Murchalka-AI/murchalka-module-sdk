using Murchalka.ModuleSdk.Testing;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: murchalka-conformance <module-root>");
    return 2;
}

var report = new ModuleRepositoryConformance().Validate(args[0]);
foreach (var finding in report.Findings)
{
    Console.WriteLine($"{finding.Severity}: {finding.Check}/{finding.Code} {finding.Location}: {finding.Message}");
}

Console.WriteLine(report.Passed
    ? "PASS: module repository satisfies Phase 0 schema and semantic conformance."
    : $"FAIL: {report.ErrorCount} conformance error(s).");
return report.Passed ? 0 : 1;
