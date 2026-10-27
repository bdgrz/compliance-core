using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

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
