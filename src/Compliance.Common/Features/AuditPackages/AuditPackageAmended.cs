using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records a new package version linked to the exact version it amends.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-amended", 1)]
public sealed record AuditPackageAmended(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
