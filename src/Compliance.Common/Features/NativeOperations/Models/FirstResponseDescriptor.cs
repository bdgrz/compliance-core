namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Immutable original write response recorded on root native admission.</summary>
public sealed record FirstResponseDescriptor(int HttpStatus, string ContentType, string ResultSchema,
    ResponseBodyReference Body);
