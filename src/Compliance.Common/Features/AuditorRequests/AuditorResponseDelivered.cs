using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The exact version of a response was intentionally delivered. <paramref name="Change" /> is the
///     <see cref="AuditorResponseDeliveryView" />; the delivery is immutable.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-response-delivered", 1)]
public sealed record AuditorResponseDelivered(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation, long Version,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
