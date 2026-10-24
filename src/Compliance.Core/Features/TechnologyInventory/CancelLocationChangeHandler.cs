using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Atomically cancels the exact pending location change after a definite failure and fences its local apply. An
///     already applied change recovers forward and the cancellation is a terminal no-op.
/// </summary>
public sealed class CancelLocationChangeHandler(NamedRecordReceivers receivers)
    : IRequestHandler<CancelLocationChange, ReceiverOutcome>
{
    /// <inheritdoc />
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CancelLocationChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return receivers.CancelAsync(LocationNames.Descriptor, request.CommandId, request.TenantId, request.RecordId,
            request.ChangeOperationId, request.ExpectedRevision, request.DefiniteFailureProof,
            (Location record, CancelAttempt attempt) => record.CancelChange(attempt.Operation, attempt.FailureProof),
            context, ct);
    }
}
