using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Only the successful verified output of one admitted build; attempts and failures are technical state.</summary>
public sealed record AuditPackageBuiltChange(Uuid BuildId, AuditPackageBuildOutput Output, DateTimeOffset CompletedAt);
