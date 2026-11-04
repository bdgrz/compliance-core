using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
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
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Projections;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
namespace Bdgrz.Compliance.Features.Remediation.History;

/// <summary>Verifies full immutable finding history transiently at a compact owner's exact hydrated cut.</summary>
public static class FindingHistory
{
    /// <summary>Reads complete history and verifies scope, predecessor, unique identities and current compact authority.</summary>
    public static async Task<FindingHistorySnapshot> ReadAsync(IDomainEventReader events, FindingAggregate owner,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 && (owner.Latest is null ||
            ContentDigest.Sha256(facts[^1], facts[^1].GetType()) != ContentDigest.Sha256(owner.Latest, owner.Latest.GetType())))
            throw Invalid("The finding history differs from its exact hydrated cut.");
        var operations = new HashSet<Uuid>();
        var actions = new HashSet<Uuid>();
        var resolvedActions = new HashSet<Uuid>();
        var closures = new HashSet<Uuid>();
        var proposalRevisions = new Dictionary<Uuid, long>();
        var proposalAuthors = new Dictionary<Uuid, HashSet<Uuid>>();
        long generation = 0;
        FindingClosureAccepted? pending = null;
        FindingClosureProposed? proposal = null;
        FindingClosedV2? closed = null;
        for (var index = 0; index < facts.Count; index++)
        {
            var fact = facts[index];
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !operations.Add(native.Operation.LogicalId))
                throw Invalid("The finding history has another owner or repeats a logical operation.");
            var operation = native.Operation;
            var revision = index + 1L;
            var writable = index > 0 && pending is null && closed is null && proposal is null;
            var valid = false;
            switch (fact)
            {
                case FindingRaisedV2 raised:
                    valid = index == 0 && Scope(raised.TenantId, raised.ProgramId, raised.FindingId) &&
                        raised.Revision == 1 && operation.LogicalId == owner.FindingId &&
                        raised.RaisedBy == operation.Actor && raised.RaisedAt == operation.AcceptedAt &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, operation.CommandKind, null);
                    generation = 1;
                    break;
                case FindingRevisedV2 revised:
                    valid = writable && Scope(revised.TenantId, revised.ProgramId, revised.FindingId) &&
                        revised.Revision == revision && revised.RevisedBy == operation.Actor &&
                        revised.RevisedAt == operation.AcceptedAt &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ReviseFinding), (ulong)index);
                    break;
                case CorrectiveActionAdmitted admitted:
                    valid = writable && admitted.Owner == owner.Stream && admitted.Revision == revision &&
                        admitted.Generation == generation && actions.Add(operation.LogicalId) &&
                        (NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(AddCorrectiveAction), (ulong)index) ||
                            index == 1 && owner.Raised is { CreationKind: "routed" } initial &&
                            operation.CommandKind == "AdmitCorrectiveAction" && operation.RootOperationId == owner.FindingId &&
                            operation.Actor == initial.Operation.Actor && operation.Episode == initial.Operation.Episode &&
                            operation.AuthoredIntentSha256 == initial.Operation.AuthoredIntentSha256 &&
                            operation.OriginalExpectedRevision is null && operation.CausationEventId is null &&
                            (operation.FirstOwner ?? operation.Owner) == owner.Stream &&
                            operation.AffectedOwner == owner.Stream && operation.ExpectedRevisionOwner == owner.Stream);
                    break;
                case FindingAcceptanceLinkedV2 linked:
                    valid = writable && Scope(linked.TenantId, linked.ProgramId, linked.FindingId) &&
                        linked.Revision == revision &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(LinkFindingAcceptance), (ulong)index);
                    break;
                case FindingCorrectiveActionResolved resolved:
                    valid = writable && Scope(resolved.TenantId, resolved.ProgramId, resolved.FindingId) &&
                        resolved.Revision == revision && resolved.RequestId == operation.LogicalId &&
                        resolved.Outcome is "completed" or "cancelled" && actions.Contains(resolved.ActionId) &&
                        resolvedActions.Add(resolved.ActionId) && resolved.Source.EventId != Uuid.Empty &&
                        resolved.Source.AggregateVersion > 0 && resolved.Source.Stream ==
                        new CorrectiveAction(owner.TenantId, owner.ProgramId, owner.FindingId, resolved.ActionId).Stream &&
                        resolved.SourceProof.Schema == SourceFactProofs.RequiredFactsSchema &&
                        operation.LogicalId == Uuid.CreateVersion5(resolved.Source.EventId, nameof(RecordCorrectiveActionOutcome)) &&
                        operation.CausationEventId == resolved.Source.EventId && operation.Actor.Kind == "system_process" &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation with { CausationEventId = null },
                            nameof(RecordCorrectiveActionOutcome), null);
                    break;
                case FindingClosureAccepted accepted:
                    valid = index > 0 && pending is null && closed is null && resolvedActions.Count == actions.Count && accepted.Owner == owner.Stream &&
                        accepted.Revision == revision && accepted.Generation == generation &&
                        (accepted.Review is null ? writable && actions.Count > 0 &&
                            NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(CloseFinding), (ulong)index) :
                            proposal is not null && accepted.Review.ReviewedBy == operation.Actor &&
                            accepted.Review.ReviewedAt == operation.AcceptedAt &&
                            accepted.Intent.Schema == ClosureIntent.Schema &&
                            FindingClosureProposalRules.CanReview(owner.TenantId, owner.FindingId, proposal,
                                proposalAuthors[proposal.ClosureId], accepted.Review, true) &&
                            NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ReviewFindingClosure), (ulong)index)) &&
                        closures.Add(operation.RootOperationId);
                    pending = accepted;
                    break;
                case FindingClosureProposed proposed:
                    valid = writable && Scope(proposed.TenantId, proposed.ProgramId, proposed.FindingId) &&
                        proposed.Revision == revision && proposed.RequestId == operation.LogicalId &&
                        proposed.ClosureId != Uuid.Empty && resolvedActions.Count == actions.Count &&
                        proposed.ProposalRevision == checked(proposalRevisions.GetValueOrDefault(proposed.ClosureId) + 1) &&
                        FindingClosureProposalRules.MemberId(proposed.ProposedBy) != Uuid.Empty &&
                        proposed.ProposedBy == operation.Actor && proposed.ProposedAt == operation.AcceptedAt &&
                        FindingClosureProposalRules.ValidTerms(proposed.Terms) &&
                        proposed.Terms.VerificationRationale == proposed.Terms.VerificationRationale.Trim() &&
                        proposed.Terms.Rationale == proposed.Terms.Rationale.Trim() &&
                        FindingClosureProposalRules.ValidActions(proposed.ActionsReliedOn, actions.Count) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ProposeFindingClosure), (ulong)index);
                    proposal = proposed;
                    proposalRevisions[proposed.ClosureId] = proposed.ProposalRevision;
                    if (!proposalAuthors.TryGetValue(proposed.ClosureId, out var authors))
                    {
                        authors = [];
                        proposalAuthors.Add(proposed.ClosureId, authors);
                    }
                    authors.Add(FindingClosureProposalRules.MemberId(proposed.ProposedBy));
                    break;
                case FindingClosureWithdrawn withdrawn:
                    valid = proposal is not null && pending is null && closed is null &&
                        Scope(withdrawn.TenantId, withdrawn.ProgramId, withdrawn.FindingId) &&
                        withdrawn.Revision == revision && withdrawn.RequestId == operation.LogicalId &&
                        withdrawn.ClosureId == proposal.ClosureId && withdrawn.ProposalRevision == proposal.ProposalRevision &&
                        withdrawn.WithdrawnBy == operation.Actor && withdrawn.WithdrawnAt == operation.AcceptedAt &&
                        FindingClosureProposalRules.MemberId(withdrawn.WithdrawnBy) == FindingClosureProposalRules.MemberId(proposal.ProposedBy) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(WithdrawFindingClosure), (ulong)index);
                    proposal = null;
                    break;
                case FindingClosureReturned returned:
                    valid = proposal is not null && pending is null && closed is null &&
                        Scope(returned.TenantId, returned.ProgramId, returned.FindingId) &&
                        returned.Revision == revision && returned.RequestId == operation.LogicalId &&
                        returned.Review.ReviewedBy == operation.Actor && returned.Review.ReviewedAt == operation.AcceptedAt &&
                        FindingRules.IsBoundedText(returned.Comments, FindingRules.MaximumTextLength) && returned.Comments == returned.Comments.Trim() &&
                        FindingClosureProposalRules.CanReview(owner.TenantId, owner.FindingId, proposal,
                            proposalAuthors[proposal.ClosureId], returned.Review, false) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ReviewFindingClosure), (ulong)index);
                    proposal = null;
                    break;
                case FindingClosedV2 finished:
                    valid = pending is not null && Scope(finished.TenantId, finished.ProgramId, finished.FindingId) &&
                        finished.Revision == revision && finished.FindingGeneration == generation &&
                        finished.Intent == pending.Intent && finished.Review == pending.Review &&
                        (finished.Review is null || proposal is not null && finished.Review.ClosureId == proposal.ClosureId &&
                            finished.Review.ProposalRevision == proposal.ProposalRevision) &&
                        Child(operation, pending, nameof(CompleteFindingClosure));
                    pending = null;
                    proposal = null;
                    closed = finished;
                    break;
                case FindingClosureCanceled canceled:
                    valid = pending is not null && canceled.Owner == owner.Stream && canceled.Revision == revision &&
                        canceled.Generation == generation && canceled.Intent == pending.Intent &&
                        Child(operation, pending, nameof(AbortFindingClosure));
                    pending = null;
                    break;
                case FindingReopenedV2 reopened:
                    valid = index > 0 && closed is not null && Scope(reopened.TenantId, reopened.ProgramId,
                        reopened.FindingId) && reopened.Revision == revision &&
                        reopened.ReopenedBy == operation.Actor && reopened.ReopenedAt == operation.AcceptedAt &&
                        FindingRules.IsBoundedText(reopened.Reason, FindingRules.MaximumTextLength) &&
                        (reopened.PreviousClosureId is null || reopened.PreviousClosureId == (closed.Review?.ClosureId ?? closed.RequestId)) &&
                        (reopened.PreviousProposalRevision is null || reopened.PreviousProposalRevision == closed.Review?.ProposalRevision) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ReopenFinding), (ulong)index);
                    closed = null;
                    generation++;
                    break;
            }
            if (!valid)
                throw Invalid("The finding history contains another scope, predecessor, action identity or closure generation.");
        }
        if (generation != owner.Generation || actions.Count != owner.AdmittedCount ||
            actions.Count - resolvedActions.Count != owner.UnresolvedActionCount ||
            pending?.Metadata.EventId != owner.PendingClosure?.Metadata.EventId ||
            proposal?.Metadata.EventId != owner.ClosureProposal?.Metadata.EventId ||
            closed?.Metadata.EventId != owner.Closed?.Metadata.EventId)
            throw Invalid("The finding history does not reproduce current compact authority.");
        return new FindingHistorySnapshot(facts);

        bool Scope(Uuid tenant, Uuid program, Uuid finding) => tenant == owner.TenantId &&
            program == owner.ProgramId && finding == owner.FindingId;
        bool Child(OperationProvenance operation, FindingClosureAccepted admission, string kind) =>
            operation.Owner == owner.Stream && (operation.FirstOwner ?? operation.Owner) == owner.Stream &&
            operation.AffectedOwner == owner.Stream && operation.ExpectedRevisionOwner == owner.Stream &&
            operation.RootOperationId == admission.Operation.RootOperationId && operation.CommandKind == kind &&
            operation.CausationEventId == admission.Metadata.EventId && operation.Actor == admission.Operation.Actor &&
            operation.Episode == admission.Operation.Episode && operation.OriginalExpectedRevision == admission.Operation.OriginalExpectedRevision &&
            operation.AuthoredIntentSha256 == admission.Operation.AuthoredIntentSha256 && operation.AcceptedAt != default;
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
