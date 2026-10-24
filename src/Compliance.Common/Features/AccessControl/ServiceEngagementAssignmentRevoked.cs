using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One actual assignment of an accepted service engagement revoked, or the whole engagement closed when
///     <paramref name="StaffMemberId" /> is null. <paramref name="Revision" /> is the engagement revision after it.
/// </summary>
[Discriminator("bdgrz.service-engagement.assignment-revoked", 1)]
public sealed record ServiceEngagementAssignmentRevoked(Uuid TenantId, Uuid OperationId, Uuid EngagementId,
    Uuid? StaffMemberId, string Reason, string Intent, ActorReference Actor, DateTimeOffset RecordedAt,
    OperationProvenance Operation, ulong Revision, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
