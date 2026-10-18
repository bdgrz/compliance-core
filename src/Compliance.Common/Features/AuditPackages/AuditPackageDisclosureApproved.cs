using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records rationale-backed disclosure approval for one exact package version and recipient.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-disclosure-approved", 1)]
public sealed record AuditPackageDisclosureApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
