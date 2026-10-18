using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records monotonic worker progress for one package build attempt.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-build-progress-recorded", 1)]
public sealed record AuditPackageBuildProgressRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
