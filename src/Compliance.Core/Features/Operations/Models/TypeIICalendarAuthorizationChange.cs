using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

public sealed record TypeIICalendarAuthorizationChange(OperationProvenance Operation, ImmutableContentRef Intent,
    Uuid PeriodId, TypeIIRangeOperationKind Kind, ObservationRange NewRange, Uuid? PriorOperationId);
