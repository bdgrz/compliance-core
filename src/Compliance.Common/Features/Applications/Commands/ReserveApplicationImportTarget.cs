using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Commands;

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
