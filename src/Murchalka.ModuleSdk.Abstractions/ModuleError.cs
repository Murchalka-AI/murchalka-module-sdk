namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Describes a structured module error.</summary>
/// <param name="Code">The machine-readable code.</param><param name="Category">The error category.</param>
/// <param name="Retryable">Whether retrying may succeed.</param><param name="Message">The human-readable message.</param>
public sealed record ModuleError(string Code, ModuleErrorCategory Category, bool Retryable, string Message);
