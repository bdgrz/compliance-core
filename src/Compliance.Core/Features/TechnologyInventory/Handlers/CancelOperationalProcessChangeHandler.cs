using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Atomically cancels the exact pending operational process change after a definite failure and fences its local apply. An
///     already applied change recovers forward and the cancellation is a terminal no-op.
/// </summary>
public sealed class CancelOperationalProcessChangeHandler(NamedRecordReceivers receivers)
    : IRequestHandler<CancelOperationalProcessChange, ReceiverOutcome>
{
    /// <inheritdoc />
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CancelOperationalProcessChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return receivers.CancelAsync(OperationalProcessNames.Descriptor, request.CommandId, request.TenantId, request.RecordId,
            request.ChangeOperationId, request.ExpectedRevision, request.DefiniteFailureProof,
            (OperationalProcess record, CancelAttempt attempt) => record.CancelChange(attempt.Operation, attempt.FailureProof),
            context, ct);
    }
}
