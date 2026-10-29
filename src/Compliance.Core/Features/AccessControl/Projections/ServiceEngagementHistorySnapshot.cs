using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Verified transient chronology; the owner itself retains only current/live/selected authority.</summary>
public sealed record ServiceEngagementHistorySnapshot(
    ServiceEngagementIndependence Owner,
    IReadOnlyList<ServiceEngagementMutationRecorded> Changes,
    IReadOnlyList<ImmutableContentRef> AcceptanceHistory,
    IReadOnlyList<DirectoryIndependenceReevaluated> DirectoryReceipts,
    IReadOnlyDictionary<Uuid, EngagementAcceptanceRecord> Operations,
    IReadOnlyDictionary<Uuid, EngagementPersonalDecisionRecord> Decisions)
{
    public EngagementAcceptanceRecord? OperationOf(Uuid operationId) => Operations.GetValueOrDefault(operationId);
    public EngagementPersonalDecisionRecord? DecisionOf(Uuid decisionId) => Decisions.GetValueOrDefault(decisionId);
    public EngagementPersonalDecisionRecord? CurrentDecision(EngagementPersonalDecisionKind kind) =>
        Decisions.Values.Where(value => value.Materialized is not null && value.Admitted.Operation.CommandKind ==
            (kind == EngagementPersonalDecisionKind.ManagementAcknowledgement
                ? ServiceEngagementIndependence.ManagementKind : ServiceEngagementIndependence.PartnerKind))
            .MaxBy(value => value.Ordinal);
}
