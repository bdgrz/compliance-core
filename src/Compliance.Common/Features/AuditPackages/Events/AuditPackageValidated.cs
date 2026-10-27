using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Events;

/// <summary>One accepted manifest delta; complete business content remains immutable and verified.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-validated", 1)]
public sealed record AuditPackageValidated(Uuid TenantId, Uuid SubjectId, Uuid ProgramId, long Revision,
    long PackageVersion, ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid? ActorMemberId) : DomainEvent, IAuditPackageManifestFact;
