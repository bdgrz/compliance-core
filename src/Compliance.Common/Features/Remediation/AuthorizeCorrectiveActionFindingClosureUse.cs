using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Holds one completed corrective action for a finding closure. <paramref name="SourceContentEventId" /> and
///     <paramref name="ExpectedContentRevision" /> name the action fact the process pinned; the receiver refuses and
///     fences when the action is no longer exactly that, is not completed, or violates the closure's independence
///     rules. An action that is not materialized yet is a transient wait, never a closure.
/// </summary>
[Discriminator("bdgrz.corrective-action.authorize-finding-closure-use", 1)]
public sealed record AuthorizeCorrectiveActionFindingClosureUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, ulong ExpectedContentRevision, Uuid ReceivingOwnerId,
    Uuid ProgramId)
    : ISourceUseAuthorization, IFindingReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
