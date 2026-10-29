using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditPackages.Events;

/// <summary>One accepted manifest delta; complete business content remains immutable and verified.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-created", 1)]
public sealed record AuditPackageCreated(Uuid TenantId, Uuid SubjectId, Uuid ProgramId, long Revision,
    long PackageVersion, ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid? ActorMemberId) : DomainEvent, IAuditPackageManifestFact;
