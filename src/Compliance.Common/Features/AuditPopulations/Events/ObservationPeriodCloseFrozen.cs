using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Compact native period authority advance with independently verified immutable current content.</summary>
[Discriminator("bdgrz.auditpopulations.observation-period-close-frozen", 1)]
public sealed record ObservationPeriodCloseFrozen(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ImmutableContentRef Change,
    DateTimeOffset OccurredAt,
    OperationProvenance Operation,
    Uuid CalendarAuthorizationId,
    long CalendarGeneration,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult,
    long NativeSequence,
    TypeIIPeriodAuthorityHeader Header,
    ImmutableContentRef CurrentContent,
    ImmutableContentRef CalendarIntent) : DomainEvent, ITypeIIPeriodStateFact;
