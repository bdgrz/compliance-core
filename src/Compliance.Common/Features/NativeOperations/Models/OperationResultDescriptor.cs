namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Authorized pointer to the immutable original result of an operation.</summary>
public sealed record OperationResultDescriptor(string Schema, string ResultUrl);
