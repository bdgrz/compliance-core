using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Settles the use of the ratification catalog after the irreversible personal decision (irreversible decision) or after
///     cancellation (canceled). <see cref="Source" /> is the accepted admission fact; the receiver resolves the
///     terminal fact from the engagement owner and never infers one from missing native state.
/// </summary>
[Discriminator("bdgrz.independence-rule-ratification-catalog.settle-independence-rule-ratification-catalog-engagement-use", 1)]
public sealed record SettleIndependenceRuleRatificationCatalogEngagementUse(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid SourceOwnerId,
    Uuid EngagementId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IEngagementPersonalDecisionUse, IEngagementPersonalDecisionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
