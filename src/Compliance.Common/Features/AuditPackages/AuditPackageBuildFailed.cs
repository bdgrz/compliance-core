using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records a failed package build while preserving its retryable attempt history.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-build-failed", 1)]
public sealed record AuditPackageBuildFailed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
