using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records delivery of the exact approved archive to its approved recipient.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-delivered", 1)]
public sealed record AuditPackageDelivered(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
