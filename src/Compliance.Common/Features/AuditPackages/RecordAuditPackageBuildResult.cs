using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records one exact package build worker result caused by its persisted request event.</summary>
[Discriminator("bdgrz.audit-packages.record-audit-package-build-result", 2)]
public sealed record RecordAuditPackageBuildResult(Uuid TenantId, Uuid ProgramId, Uuid PackageId,
    Uuid BuildAttemptId, Uuid BuildRequestEventId, AuditPackageBuildOutput Output,
    DateTimeOffset CompletedAt) : IRequest;
