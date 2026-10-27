using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>
///     One reservation of the program calendar, keyed by its root operation. <paramref name="Generation" /> is the
///     calendar's own monotonic counter at the grant (zero for a fence). A fence has no range.
/// </summary>
public sealed record TypeIIRangeReservation(Uuid OperationId, Uuid PeriodId, TypeIIRangeReservationState State,
    TypeIIRangeOperationKind Kind, ObservationRange? Range, long Generation, Uuid? PriorOperationId,
    TypeIIPeriodRangeAuthorized? AuthorizedFact, TypeIIPeriodRangeSettled? SettledFact)
{
    /// <summary>True while the range blocks overlapping ranges of other periods.</summary>
    public bool IsLive => State is TypeIIRangeReservationState.Pending or TypeIIRangeReservationState.Active;
}
