using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The frozen terms of one opening or amendment, persisted by the period before the calendar is touched. An
///     opening has no prior grant and no old range; an amendment holds the old range through its exact prior grant
///     and reserves the new one under <paramref name="NewReservationKey" />. Every receiver copies the header.
/// </summary>
public sealed record TypeIIRangeIntent(DurableIntentHeader Header, Uuid ProgramId, Uuid PeriodId,
    TypeIIRangeOperationKind OperationKind, ReservationGrant? PriorGrant, ObservationRange? OldRange,
    ObservationRange NewRange, ImmutableContentRef OpeningOrAmendmentContent, ReservationKey NewReservationKey)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "type-i-i-range-intent.v1";
}
