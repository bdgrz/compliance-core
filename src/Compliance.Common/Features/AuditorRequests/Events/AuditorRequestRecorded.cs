using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The auditor request was recorded as the root native fact of its operation. <paramref name="Change" /> is the
///     verified immutable <c>AuditorRequestTermsContent</c> of the first terms; the request starts at generation one.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-request-recorded", 1)]
public sealed record AuditorRequestRecorded(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
