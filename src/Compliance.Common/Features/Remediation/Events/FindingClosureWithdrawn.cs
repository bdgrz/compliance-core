using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>The proposer withdrew this exact proposal revision, fencing any accepted but unclosed review.</summary>
[Discriminator("bdgrz.finding.closure.withdrawn", 1)]
public sealed record FindingClosureWithdrawn(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid ClosureId, long ProposalRevision, ActorReference WithdrawnBy, DateTimeOffset WithdrawnAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult,
    FindingClosureReviewCancellation? CanceledReview = null)
    : DomainEvent, INativeOperationFact;
