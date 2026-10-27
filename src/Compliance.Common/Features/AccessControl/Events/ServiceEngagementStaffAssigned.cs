using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One actual staff assignment was recorded on an accepted engagement after the partner authority, the service
///     epoch and the staff and canonical-user practice walls were authorized and committed. The directory snapshots the
///     assignment relied on are retained as immutable content. <paramref name="Revision" /> is the engagement revision
///     after it, and <paramref name="OperationResult" /> is the terminal result: the complete revised acceptance view.
/// </summary>
[Discriminator("bdgrz.access-control.service-engagement.staff-assigned", 1)]
public sealed record ServiceEngagementStaffAssigned(Uuid TenantId, Uuid OperationId, Uuid EngagementId,
    ImmutableContentRef AssignedStaffContent, DateOnly EffectiveFrom, ImmutableContentRef PartnerStaffContent,
    ImmutableContentRef PartnerDesignationContent, long PartnerDutyContinuityStartRevision, string Intent,
    ActorReference Actor, DateTimeOffset RecordedAt, OperationProvenance Operation, ulong Revision,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
