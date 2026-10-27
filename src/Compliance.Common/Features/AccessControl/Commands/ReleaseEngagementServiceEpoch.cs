using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Releases the held service epoch after the operation recorded its terminal fact (irreversible decision) or was
///     rejected (canceled). <see cref="Source" /> is that terminal fact; the receiver resolves it from the engagement
///     owner and never infers a terminal fact from missing native state. Permanent practice commitments remain.
/// </summary>
[Discriminator("bdgrz.client-service-independence.release-engagement-service-epoch", 1)]
public sealed record ReleaseEngagementServiceEpoch(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid EngagementId,
    long ServiceEpoch, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IEngagementPersonalDecisionUse, IEngagementAcceptanceReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;

    Uuid ISourceUseSettlement.SourceOwnerId => Uuid.Parse(Target.Resource, System.Globalization.CultureInfo.InvariantCulture);
}
