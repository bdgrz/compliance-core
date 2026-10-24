using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>The independently materialized historical handoff with its immutable original terminal result.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-delivered", 1)]
public sealed record AuditPackageDelivered(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid ProgramId, Uuid PackageId,
    long PackageVersion, Uuid DeliveryId, NativeFactReference Admission) : DomainEvent, INativeOperationFact;
