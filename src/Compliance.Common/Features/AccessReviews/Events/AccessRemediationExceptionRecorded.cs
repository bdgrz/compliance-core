using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>An exception to verified remediation was approved; the complete exception is verified content.</summary>
[Discriminator("bdgrz.access_review.remediation.exception_recorded", 2)]
public sealed record AccessRemediationExceptionRecorded(Uuid TenantId, Uuid CampaignId, long Revision, Uuid ItemId,
    ImmutableContentRef ExceptionContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
