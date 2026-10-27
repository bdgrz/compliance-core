using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Acquires the exact draft rule version the partner evaluation relies on for one personal engagement decision. <see cref="Source" /> is the accepted admission fact;
///     the receiver verifies it by hydrating the engagement owner and never trusts a caller-supplied policy value.
/// </summary>
[Discriminator("bdgrz.independence-rule-catalog.authorize-independence-rule-catalog-engagement-use", 1)]
public sealed record AuthorizeIndependenceRuleCatalogEngagementUse(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid SourceOwnerId,
    Uuid SourceContentEventId, ulong ExpectedContentRevision, Uuid EngagementId)
    : ISourceUseAuthorization, IEngagementPersonalDecisionUse, IEngagementPersonalDecisionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
