using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Transient verified period rule mutation; never emitted or retained by the command owner.</summary>
public sealed record ObservationPeriodStartedBody(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt,
    ImmutableContentRef Change,
    OperationProvenance Operation,
    Uuid CalendarAuthorizationId,
    long CalendarGeneration,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult);
