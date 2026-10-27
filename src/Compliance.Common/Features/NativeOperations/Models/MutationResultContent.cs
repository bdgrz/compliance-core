namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Exact original normalized typed result of a mutation.</summary>
public sealed record MutationResultContent(string Schema, ResponseBodyReference Value);
