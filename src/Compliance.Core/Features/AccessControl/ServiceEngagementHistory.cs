using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reconstructs complete transient engagement history at the actual hydrated native/physical prefix.</summary>
public static class ServiceEngagementHistory
{
    public static async Task<ServiceEngagementHistorySnapshot> ReadAsync(IDomainEventReader events,
        ServiceEngagementIndependence owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var model = new ServiceEngagementHistoryModel(owner.TenantId, owner.EngagementId);
        var logicalIds = new HashSet<Uuid>();
        var digest = FirmIndependenceReplayDigest.Empty;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logicalIds.Add(native.Operation.LogicalId) ||
                (native.Operation.FirstOwner ?? native.Operation.Owner) != owner.Stream ||
                native.Operation.AffectedOwner != owner.Stream || native.Operation.ExpectedRevisionOwner != owner.Stream)
                throw Invalid("The engagement history contains another owner, routing identity or repeated logical operation.");
            if (!HasExpectedPinnedRevision(fact, model.Revision))
                throw Invalid("An engagement child names another pinned content revision or generation.");
            if (!HasExpectedCommand(fact, native.Operation.CommandKind))
                throw Invalid("The engagement fact is tagged with another receiver command kind.");
            var operation = native.Operation;
            var root = operation.LogicalId == operation.RootOperationId;
            if (root)
            {
                var revision = fact is DirectoryIndependenceReevaluated ? (ulong?)null : model.Revision;
                if (!NativeOwnerHistory.IsOwnRoot(owner, operation, operation.CommandKind, revision) ||
                    fact is not (ServiceEngagementMutationRecorded or ServiceEngagementAssignmentRevoked or
                        EngagementPersonalDecisionAdmitted or ServiceEngagementAcceptanceRequested or
                        ServiceEngagementStaffAssignmentRequested or DirectoryIndependenceReevaluated))
                    throw Invalid("The engagement root differs from its immutable command scope, original revision or episode.");
            }
            else
            {
                var admitted = (DomainEvent?)model.DecisionOf(operation.RootOperationId)?.Admitted ??
                    model.OperationOf(operation.RootOperationId)?.Requested;
                if (admitted is not INativeOperationFact original || operation.CausationEventId is null ||
                    operation.AuthoredIntentSha256 != original.Operation.AuthoredIntentSha256 ||
                    operation.Actor != original.Operation.Actor ||
                    operation.OriginalExpectedRevision != original.Operation.OriginalExpectedRevision ||
                    operation.Episode != original.Operation.Episode)
                    throw Invalid("An engagement child differs from its exact admitted root and original actor/intent/revision.");
            }
            if (operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected engagement receipt differs from the exact native payload and metadata.");
            digest = FirmIndependenceReplayDigest.Append(digest, fact);
            try
            {
                model.Observe(fact);
            }
            catch (InvalidOperationException error)
            {
                throw Invalid($"The engagement chronology cannot be verified: {error.Message}");
            }
        }
        if (model.Revision != owner.Revision || model.Status != owner.Status || model.LatestResult != owner.LatestResult ||
            model.AcceptanceResult != owner.AcceptanceResult ||
            !Same(model.DecisionOf(owner.SelectedRootId), owner.DecisionOf(owner.SelectedRootId)) ||
            !Same(model.OperationOf(owner.SelectedRootId), owner.OperationOf(owner.SelectedRootId)) ||
            !Same(model.CurrentDecision(EngagementPersonalDecisionKind.ManagementAcknowledgement), owner.CurrentDecision(EngagementPersonalDecisionKind.ManagementAcknowledgement)) ||
            !Same(model.CurrentDecision(EngagementPersonalDecisionKind.PartnerEvaluation), owner.CurrentDecision(EngagementPersonalDecisionKind.PartnerEvaluation)) ||
            model.Decisions.Values.Count(value => value.IsPending) != owner.LiveDecisions.Count ||
            model.Operations.Values.Count(value => value.IsPending) != owner.LiveOperations.Count ||
            owner.LiveDecisions.Any(pair => !Same(pair.Value, model.DecisionOf(pair.Key))) ||
            owner.LiveOperations.Any(pair => !Same(pair.Value, model.OperationOf(pair.Key))))
            throw Invalid("The engagement full history differs from compact current, live or selected authority.");
        owner.ReplayCut.Confirm(owner, digest);
        return new ServiceEngagementHistorySnapshot(owner, model.Changes, model.AcceptanceHistory,
            model.DirectoryReceipts, model.Operations, model.Decisions);
    }

    static bool HasExpectedPinnedRevision(DomainEvent fact, ulong revision) => fact switch
    {
        EngagementPersonalDecisionAuthorized value => value.Revision == (long)revision && value.Generation == ServiceEngagementIndependence.DecisionGeneration,
        EngagementPersonalDecisionCanceled value => value.Revision == (long)revision && value.Generation == ServiceEngagementIndependence.DecisionGeneration,
        EngagementPersonalDecisionMaterialized value => value.Revision == (long)revision && value.Generation == ServiceEngagementIndependence.DecisionGeneration,
        EngagementPracticeCommitmentsAuthorized value => value.Revision == (long)revision && value.Generation == ServiceEngagementIndependence.AcceptanceGeneration,
        ServiceEngagementAcceptanceRejected value => value.Revision == (long)revision && value.Generation == ServiceEngagementIndependence.AcceptanceGeneration,
        ServiceEngagementStaffAssignmentRejected value => value.Revision == (long)revision && value.Generation == ServiceEngagementIndependence.AcceptanceGeneration,
        _ => true,
    };

    static bool HasExpectedCommand(DomainEvent fact, string command) => fact switch
    {
        ServiceEngagementMutationRecorded value => command == value.Intent,
        ServiceEngagementAssignmentRevoked => command == nameof(RevokeServiceEngagementActualStaff),
        EngagementPersonalDecisionAdmitted => command is ServiceEngagementIndependence.ManagementKind or ServiceEngagementIndependence.PartnerKind,
        EngagementPersonalDecisionAuthorized => command == nameof(AuthorizeEngagementPersonalDecision),
        EngagementPersonalDecisionCanceled => command == nameof(CancelEngagementPersonalDecision),
        EngagementPersonalDecisionMaterialized => command == nameof(SettleEngagementPersonalDecision),
        ServiceEngagementAcceptanceRequested => command == nameof(AcceptServiceEngagement),
        ServiceEngagementStaffAssignmentRequested => command == nameof(AssignServiceEngagementStaff),
        EngagementPracticeCommitmentsAuthorized => command == nameof(AuthorizeEngagementPracticeCommitments),
        ServiceEngagementAcceptanceRejected => command == nameof(RejectServiceEngagementAcceptance),
        ServiceEngagementStaffAssignmentRejected => command == nameof(RejectServiceEngagementStaffAssignment),
        ServiceEngagementAcceptanceRecorded => command == nameof(CompleteServiceEngagementAcceptance),
        ServiceEngagementStaffAssigned => command == nameof(CompleteServiceEngagementStaffAssignment),
        DirectoryIndependenceReevaluated => command == nameof(RecordDirectoryIndependenceReevaluation),
        _ => false,
    };

    static bool Same(EngagementPersonalDecisionRecord? a, EngagementPersonalDecisionRecord? b) =>
        a is null ? b is null : b is not null && a.Ordinal == b.Ordinal && a.Admitted == b.Admitted &&
        a.Authorized == b.Authorized && a.Canceled == b.Canceled && a.Materialized == b.Materialized;
    static bool Same(EngagementAcceptanceRecord? a, EngagementAcceptanceRecord? b) =>
        a is null ? b is null : b is not null && a.Kind == b.Kind && a.Requested.Equals(b.Requested) &&
        a.Intent == b.Intent && a.Authorized == b.Authorized && Equals(a.Rejected, b.Rejected) && Equals(a.Completed, b.Completed);
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
