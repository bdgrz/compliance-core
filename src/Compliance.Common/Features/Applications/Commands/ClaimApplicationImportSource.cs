using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>
///     Claims the canonical source of a sealed batch for exactly that batch. The claim requires the complete frozen
///     plan and the exact source revision the plan was frozen against; one batch holds a source at a time.
///     <paramref name="ExpectedRevision" /> is that frozen source revision.
/// </summary>
[Discriminator("bdgrz.application_import_source.claim", 1)]
public sealed record ClaimApplicationImportSource(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid BatchId,
    string SourceKey, string SourceNamespace)
    : IRequest<ReceiverOutcome>, IApplicationImportReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
