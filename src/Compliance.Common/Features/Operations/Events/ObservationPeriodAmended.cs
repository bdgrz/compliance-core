using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Compact native period authority advance with independently verified immutable current content.</summary>
[Discriminator("bdgrz.operations.observation-period-amended", 1)]
public sealed record ObservationPeriodAmended(
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
    ImmutableContentRef CurrentContent) : DomainEvent, ITypeIIPeriodStateFact;
