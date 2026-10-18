using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records a complete archive whose generated entries match the exact manifest.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-built", 1)]
public sealed record AuditPackageBuilt(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
