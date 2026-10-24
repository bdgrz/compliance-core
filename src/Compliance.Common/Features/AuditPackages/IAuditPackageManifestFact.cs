using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>The compact owner envelope shared by the closed set of manifest facts.</summary>
public interface IAuditPackageManifestFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid SubjectId { get; }
    Uuid ProgramId { get; }
    Uuid? ActorMemberId { get; }
    ImmutableContentRef? OperationResult { get; }
    long Revision { get; }
    long PackageVersion { get; }
    ImmutableContentRef Change { get; }
    DateTimeOffset OccurredAt { get; }
}
