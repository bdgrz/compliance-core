using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Acquires the exclusive hold of one target group in canonical stream-key order. The hold pins the group's frozen
///     initial application revision and is never upgraded; a fenced batch is refused.
/// </summary>
[Discriminator("bdgrz.declared_application.reserve_import_target", 1)]
public sealed record ReserveApplicationImportTarget(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid BatchId, Uuid ApplicationId, ImmutableContentRef GroupedRows)
    : IRequest<ReceiverOutcome>, IApplicationImportReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
