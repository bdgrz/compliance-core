using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

public sealed record TypeIICalendarSettlementChange(OperationProvenance Operation, ImmutableContentRef Intent,
    Uuid PeriodId, TypeIIRangeSettlementKind Settlement, Uuid TargetOperationId, Uuid? ReleasedOperationId);
