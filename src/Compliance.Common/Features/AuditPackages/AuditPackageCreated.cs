using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Creates the aggregate for one frozen examination scope and selected package content.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-created", 1)]
public sealed record AuditPackageCreated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
