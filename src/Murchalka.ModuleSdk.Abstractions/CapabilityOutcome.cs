namespace Murchalka.ModuleSdk.Abstractions;

/// <summary>Represents either a successful capability value or a structured error.</summary>
public readonly record struct CapabilityOutcome<T>
{
    private CapabilityOutcome(bool succeeded, T? value, ModuleError? error)
    { Succeeded = succeeded; Value = value; Error = error; }

    /// <summary>Gets whether the invocation succeeded.</summary>
    public bool Succeeded { get; }
    /// <summary>Gets the successful value.</summary>
    public T? Value { get; }
    /// <summary>Gets the failure details.</summary>
    public ModuleError? Error { get; }

    /// <summary>Creates a successful outcome.</summary>
    public static CapabilityOutcome<T> Success(T value) => new(true, value, null);

    /// <summary>Creates a failed outcome.</summary>
    public static CapabilityOutcome<T> Failure(ModuleError error)
    { ArgumentNullException.ThrowIfNull(error); return new CapabilityOutcome<T>(false, default, error); }
}
