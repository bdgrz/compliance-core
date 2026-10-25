using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
