using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Status of an asynchronous operation. Failure is the public request error.</summary>
public sealed record OperationStatusResponse(Uuid OperationId, OperationStatus State,
    OperationResultDescriptor? Result, RequestError? Failure);
