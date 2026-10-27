namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>The frozen result content of an operation whose declared result is Unit.</summary>
public sealed record UnitResult
{
    /// <summary>Schema of the frozen content.</summary>
    public const string Schema = "unit-result.v1";
}
