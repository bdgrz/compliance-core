using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

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
