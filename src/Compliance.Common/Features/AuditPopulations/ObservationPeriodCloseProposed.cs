using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Accepts the exact server-derived close reconciliation and its immutable original result.</summary>
[Discriminator("bdgrz.auditpopulations.observation-period-close-proposed", 3)]
public sealed record ObservationPeriodCloseProposed(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid? ActorMemberId, TypeIIPeriodView State, DateTimeOffset OccurredAt, ImmutableContentRef Change,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
