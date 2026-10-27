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
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
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
