using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The external disposition of the exact delivered version. <paramref name="Change" /> is the
///     <see cref="AuditorResponseDispositionView" />; the disposition is immutable.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-response-disposition-recorded", 1)]
public sealed record AuditorResponseDispositionRecorded(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid? ActorMemberId, ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    long Version, string Outcome, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
