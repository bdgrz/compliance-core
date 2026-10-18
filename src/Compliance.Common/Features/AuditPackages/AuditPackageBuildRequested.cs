using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records an attributed package-generation attempt against an exact manifest.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-build-requested", 1)]
public sealed record AuditPackageBuildRequested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPackageView State,
    DateTimeOffset OccurredAt) : DomainEvent;
