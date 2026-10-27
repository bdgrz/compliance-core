using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditPackages.Events;

/// <summary>Freezes an exact personal external-handoff attestation before independent historical materialization.</summary>
[Discriminator("bdgrz.audit-package-manifest.audit-package-delivery-admitted", 1)]
public sealed record AuditPackageDeliveryAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid TenantId, Uuid SubjectId,
    Uuid ProgramId, long PackageVersion, DateTimeOffset OccurredAt, Uuid? ActorMemberId)
    : DomainEvent, IAuditPackageManifestFact
{
    ImmutableContentRef IAuditPackageManifestFact.Change => Intent;
}
