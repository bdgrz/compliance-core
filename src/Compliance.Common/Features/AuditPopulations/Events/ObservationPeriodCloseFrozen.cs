using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditPopulations.Events;

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
