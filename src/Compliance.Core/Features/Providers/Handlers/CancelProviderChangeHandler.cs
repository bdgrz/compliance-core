using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Atomically cancels the exact pending provider change after a definite failure and fences its local apply. An
///     already applied change recovers forward and the cancellation is a terminal no-op.
/// </summary>
public sealed class CancelProviderChangeHandler(NamedRecordReceivers receivers)
    : IRequestHandler<CancelProviderChange, ReceiverOutcome>
{
    /// <inheritdoc />
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CancelProviderChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return receivers.CancelAsync(ProviderNames.Descriptor, request.CommandId, request.TenantId, request.RecordId,
            request.ChangeOperationId, request.ExpectedRevision, request.DefiniteFailureProof,
            (Provider record, CancelAttempt attempt) => record.CancelChange(attempt.Operation, attempt.FailureProof),
            context, ct);
    }
}
