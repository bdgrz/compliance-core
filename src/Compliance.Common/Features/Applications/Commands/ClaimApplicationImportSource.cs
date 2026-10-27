using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

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
