using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

public sealed record TypeIICalendarAuthorizationChange(OperationProvenance Operation, ImmutableContentRef Intent,
    Uuid PeriodId, TypeIIRangeOperationKind Kind, ObservationRange NewRange, Uuid? PriorOperationId);
