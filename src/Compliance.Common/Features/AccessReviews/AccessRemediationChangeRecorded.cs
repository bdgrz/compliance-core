using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A provider-side change was reported; the complete change is verified content.</summary>
[Discriminator("bdgrz.access_review.remediation.change_recorded", 2)]
public sealed record AccessRemediationChangeRecorded(Uuid TenantId, Uuid CampaignId, long Revision, Uuid ItemId,
    ImmutableContentRef ChangeContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
