using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The exact draft version of a response was submitted. <paramref name="Change" /> is the
///     <c>AuditorResponseSubmissionView</c> and <paramref name="Version" /> the business version it submitted.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-response-submitted", 1)]
public sealed record AuditorResponseSubmitted(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation, long Version,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
