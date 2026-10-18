using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records an actual staff assignment to an accepted engagement after current partner and directory checks.</summary>
[Discriminator("bdgrz.access-control.service-engagement.staff-assigned", 1)]
public sealed record ServiceEngagementStaffAssigned(
    Uuid TenantId,
    Uuid RequestId,
    long ExpectedSequence,
    Uuid EngagementId,
    FirmStaffMemberView AssignedStaff,
    DateOnly EffectiveFrom,
    FirmStaffMemberView PartnerStaff,
    FirmProfessionalDutyDesignationView PartnerDesignation,
    long PartnerDutyContinuityStartRevision,
    string Intent,
    ActorReference Actor,
    DateTimeOffset RecordedAt) : DomainEvent;
