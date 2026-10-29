using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.History;

/// <summary>Verified immutable action history at one hydrated owner's exact native and physical cut.</summary>
public static class RiskTreatmentActionHistory
{
    /// <summary>Reads historical facts without retaining their collections in command authority.</summary>
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events,
        RiskTreatmentAction owner, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 && (owner.Latest is null ||
            ContentDigest.Sha256(facts[^1], facts[^1].GetType()) !=
            ContentDigest.Sha256(owner.Latest, owner.Latest.GetType())))
            throw Invalid("The action history differs from its exact hydrated cut.");
        var logicalIds = new HashSet<Uuid>();
        var status = RiskStates.ActionOpen;
        long generation = 0;
        RiskTreatmentActionCompletionReviewAccepted? pending = null;
        for (var index = 0; index < facts.Count; index++)
        {
            var fact = facts[index];
            if (fact is not INativeOperationFact native || native.Operation.LogicalId == Uuid.Empty ||
                !logicalIds.Add(native.Operation.LogicalId))
                throw Invalid("An action history repeats a logical operation or has another fact type.");
            var revision = index + 1;
            var root = native.Operation;
            bool valid;
            switch (fact)
            {
                case RiskTreatmentActionAdded added:
                    valid = index == 0 && Scope(added.TenantId, added.ProgramId, added.RiskId) &&
                        added.Revision == revision && root.LogicalId == owner.ActionId &&
                        NativeOwnerHistory.IsOwnRoot(owner, root, nameof(AddRiskTreatmentAction), 0);
                    break;
                case RiskTreatmentActionRevised revised:
                    valid = index > 0 && pending is null && status == RiskStates.ActionOpen &&
                        Scope(revised.TenantId, revised.ProgramId, revised.RiskId) &&
                        revised.ActionId == owner.ActionId && revised.Revision == revision &&
                        revised.ExpectedRevision == index && revised.RequestId == root.LogicalId &&
                        revised.RevisedBy == root.Actor && revised.RevisedAt == root.AcceptedAt &&
                        NativeOwnerHistory.IsOwnRoot(owner, root, nameof(ReviseRiskTreatmentAction), (ulong)index);
                    break;
                case RiskTreatmentActionCancelled canceled:
                    valid = index > 0 && pending is null &&
                        status is RiskStates.ActionOpen or RiskStates.ActionSubmitted &&
                        Scope(canceled.TenantId, canceled.ProgramId, canceled.RiskId) &&
                        canceled.ActionId == owner.ActionId && canceled.Revision == revision &&
                        canceled.CancellationId == root.LogicalId && canceled.CancelledBy == root.Actor &&
                        canceled.CancelledAt == root.AcceptedAt &&
                        NativeOwnerHistory.IsOwnRoot(owner, root, nameof(CancelRiskTreatmentAction), (ulong)index);
                    status = RiskStates.ActionCancelled;
                    break;
                case RiskTreatmentActionCompletionSubmitted submitted:
                    valid = index > 0 && pending is null && status == RiskStates.ActionOpen &&
                        Scope(submitted.TenantId, submitted.ProgramId, submitted.RiskId) &&
                        submitted.ActionId == owner.ActionId && submitted.Revision == revision &&
                        submitted.SubmissionId == root.LogicalId && submitted.SubmittedBy == root.Actor &&
                        submitted.SubmittedAt == root.AcceptedAt && submitted.SubmitterMemberId != Uuid.Empty &&
                        submitted.SubmitterMemberId.ToString() == root.Actor.Id &&
                        NativeOwnerHistory.IsOwnRoot(owner, root, nameof(SubmitRiskTreatmentActionCompletion), (ulong)index);
                    generation++;
                    status = RiskStates.ActionSubmitted;
                    break;
                case RiskTreatmentActionCompletionReviewAccepted accepted:
                    valid = pending is null && status == RiskStates.ActionSubmitted &&
                        accepted.Owner == owner.Stream && accepted.Revision == revision &&
                        accepted.Generation == generation &&
                        NativeOwnerHistory.IsOwnRoot(owner, root, nameof(ReviewRiskTreatmentActionCompletion), (ulong)index);
                    pending = accepted;
                    break;
                case RiskTreatmentActionCompletionReviewed reviewed:
                    valid = pending is not null && Scope(reviewed.TenantId, reviewed.ProgramId, reviewed.RiskId) &&
                        reviewed.ActionId == owner.ActionId && reviewed.Revision == revision &&
                        reviewed.DecisionId == pending.Operation.RootOperationId &&
                        reviewed.Intent == pending.Intent && reviewed.Reviewer == pending.Operation.Actor &&
                        reviewed.ReviewedAt == pending.Operation.AcceptedAt &&
                        reviewed.Outcome is RiskStates.Accept or RiskStates.Reject &&
                        Child(owner, root, pending, nameof(CompleteRiskTreatmentActionReview));
                    status = reviewed.Outcome == RiskStates.Accept ? RiskStates.ActionCompleted : RiskStates.ActionOpen;
                    pending = null;
                    break;
                case RiskTreatmentActionCompletionReviewCanceled canceled:
                    valid = pending is not null && canceled.Owner == owner.Stream && canceled.Revision == revision &&
                        canceled.Generation == pending.Generation && canceled.Intent == pending.Intent &&
                        canceled.SourceProofs is not null &&
                        Child(owner, root, pending, nameof(AbortRiskTreatmentActionReview));
                    pending = null;
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
                throw Invalid("The action history contains another owner, predecessor, attribution or review source.");
        }
        if (status != owner.Status || pending?.Metadata.EventId != owner.PendingReview?.Metadata.EventId)
            throw Invalid("The action history does not reproduce current bounded authority.");
        return facts;

        bool Scope(Uuid tenant, Uuid program, Uuid risk) =>
            tenant == owner.TenantId && program == owner.ProgramId && risk == owner.RiskId;
    }

    /// <summary>Verifies the initial immutable body against its native action and original actor snapshot.</summary>
    public static async Task<RiskTreatmentActionView?> ReadInitialAsync(IImmutableContentStore content,
        RiskTreatmentAction owner, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.Added is not { } added)
            return null;
        var view = await FindingContent.ReadAsync<RiskTreatmentActionView>(content, owner.TenantId,
            added.ActionContent, ct).ConfigureAwait(false);
        if (added.ActionContent.Schema != RiskContent.ActionSchema || view.ActionId != owner.ActionId ||
            view.RiskId != owner.RiskId || view.Revision != 1 || view.Status != RiskStates.ActionOpen || view.Overdue ||
            view.Completions is null || view.Completions.Count != 0 || view.CancelledAt is not null || view.CancelledBy is not null ||
            view.CancellationRationale is not null || view.CreatedBy != added.Operation.Actor ||
            view.CreatedAt != added.Operation.AcceptedAt || view.AccountableMemberId == Uuid.Empty ||
            RiskCommands.InvalidText(view.Title) || view.Title.Length > RiskCommands.MaximumTitleLength ||
            RiskCommands.InvalidText(view.TargetState) ||
            RiskCommands.InvalidText(view.ExpectedEvidence) || view.EvidenceRequestIds is null || view.EvidenceRequestIds.Any(static id => id == Uuid.Empty) ||
            view.EvidenceRequestIds.Distinct().Count() != view.EvidenceRequestIds.Count)
            throw Invalid("The immutable initial action body belongs to another native identity or original attribution.");
        return view;
    }

    static bool Child(RiskTreatmentAction owner, OperationProvenance operation,
        RiskTreatmentActionCompletionReviewAccepted accepted, string kind) =>
        operation.Owner == owner.Stream && (operation.FirstOwner ?? operation.Owner) == owner.Stream &&
        operation.AffectedOwner == owner.Stream && operation.ExpectedRevisionOwner == owner.Stream &&
        operation.RootOperationId == accepted.Operation.RootOperationId && operation.CommandKind == kind &&
        operation.CausationEventId == accepted.Metadata.EventId && operation.Episode == accepted.Operation.Episode &&
        operation.Actor == accepted.Operation.Actor && operation.OriginalExpectedRevision == accepted.Operation.OriginalExpectedRevision &&
        operation.AuthoredIntentSha256 == accepted.Operation.AuthoredIntentSha256 && operation.AcceptedAt != default;

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
