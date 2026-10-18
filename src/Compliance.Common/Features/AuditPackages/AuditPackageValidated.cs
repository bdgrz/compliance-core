using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records findings bound to the exact package manifest and version.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-validated", 1)]
public sealed record AuditPackageValidated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
