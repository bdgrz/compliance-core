using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

public sealed record TypeIICalendarSettlementChange(OperationProvenance Operation, ImmutableContentRef Intent,
    Uuid PeriodId, TypeIIRangeSettlementKind Settlement, Uuid TargetOperationId, Uuid? ReleasedOperationId);
