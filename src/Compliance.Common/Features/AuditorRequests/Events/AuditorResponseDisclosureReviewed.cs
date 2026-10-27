using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The independent disclosure review of the exact submitted version. <paramref name="Change" /> is the
///     <see cref="AuditorResponseReviewView" />; <paramref name="Outcome" /> is approved or rejected.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-response-disclosure-reviewed", 1)]
public sealed record AuditorResponseDisclosureReviewed(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid? ActorMemberId, ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    long Version, string Outcome, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
