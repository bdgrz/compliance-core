using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The exact draft version of a response was submitted. <paramref name="Change" /> is the
///     <c>AuditorResponseSubmissionView</c> and <paramref name="Version" /> the business version it submitted.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-response-submitted", 1)]
public sealed record AuditorResponseSubmitted(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation, long Version,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
