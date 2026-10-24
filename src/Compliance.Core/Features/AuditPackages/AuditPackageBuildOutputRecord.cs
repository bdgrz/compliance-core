using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Technical successful-output checkpoint; failures, attempts and leases remain in the process record.</summary>
public sealed record AuditPackageBuildOutputRecord(Uuid TenantId, Uuid BuildId, NativeFactReference Source,
    ImmutableContentRef Intent, ImmutableContentRef VerifiedOutput);
