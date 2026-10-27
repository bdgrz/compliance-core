using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

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
