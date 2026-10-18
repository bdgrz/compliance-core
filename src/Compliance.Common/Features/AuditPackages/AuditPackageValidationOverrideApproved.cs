using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records an attributed, rationale-backed validation override.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-validation-override-approved", 1)]
public sealed record AuditPackageValidationOverrideApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
