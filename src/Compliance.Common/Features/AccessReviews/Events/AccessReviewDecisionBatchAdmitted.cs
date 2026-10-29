using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     The campaign admitted a complete bulk decision: the exact campaign revision, the frozen
///     <see cref="BulkAccessDecisionIntent" /> (the preview token, the immutable selection with each item's frozen
///     head and the shared decision and rationale) and the original 202 acknowledgment. The batch is created from this
///     fact at its own revision zero, and a campaign completion drains it before it holds any item.
/// </summary>
[Discriminator("bdgrz.access-review-campaign.access-review-decision-batch-admitted", 1)]
public sealed record AccessReviewDecisionBatchAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
