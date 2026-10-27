using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The reviewer's decision on one item. <paramref name="DecisionsManifest" /> is the complete original decision
///     collection as verified content. A decision that belongs to a bulk batch names it in
///     <paramref name="DecisionBatchId" /> and stays hidden until the batch publishes
///     (<paramref name="PublicationRequired" />); a single decision names no batch and is visible at once.
/// </summary>
[Discriminator("bdgrz.access_review.decisions.recorded", 2)]
public sealed record AccessDecisionsRecorded(Uuid TenantId, Uuid CampaignId, Uuid ItemId, long Revision,
    ImmutableContentRef DecisionsManifest, OperationProvenance Operation, Uuid? DecisionBatchId,
    bool PublicationRequired, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
