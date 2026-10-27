using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Immutable scalar reservation authority; actual source facts are resolved from their native revisions.</summary>
public sealed record TypeIICalendarReservationContent(Uuid OperationId, Uuid PeriodId, TypeIIRangeReservationState State,
    TypeIIRangeOperationKind Kind, ObservationRange? Range, long Generation, Uuid? PriorOperationId,
    long? AuthorizationRevision, long? SettlementRevision)
{
    public string? AuthorizationBindingSha256 { get; init; }
    public string? SettlementBindingSha256 { get; init; }
    public bool IsLive => State is TypeIIRangeReservationState.Pending or TypeIIRangeReservationState.Active;
}
