using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
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
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.History;

/// <summary>Complete transient batch chronology and current row choices at the exact hydrated native/physical prefix.</summary>
public static class ApplicationImportBatchHistory
{
    public static async Task<ApplicationImportBatchHistorySnapshot> ReadAsync(IDomainEventReader events,
        ApplicationImportBatch owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var logical = new HashSet<Uuid>();
        var correlations = new Dictionary<Uuid, ApplicationImportRowCorrelated>();
        var rows = new HashSet<Uuid>();
        var digest = ApplicationImportSourceDigest.Empty;
        ApplicationImportStaged? staged = null;
        ApplicationImportRetirementProposalSealed? proposal = null;
        ApplicationImportPlanSealed? plan = null;
        ApplicationImportExecutionAuthorized? authorization = null;
        ApplicationImportCancellationAccepted? cancel = null;
        ApplicationImportCommitted? committed = null;
        ApplicationImportCanceled? canceled = null;
        ApplicationImportFailed? failed = null;
        long revision = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation is null || native.Operation.Actor is null ||
                native.Operation.AuthoredIntentSha256 is not { Length: 64 } || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add(native.Operation.LogicalId))
                throw Invalid("The import batch prefix contains another owner or repeated logical operation.");
            digest = ApplicationImportSourceDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected batch acceptance differs from its exact payload and full metadata.");
            var operation = native.Operation;
            var ended = committed is not null || canceled is not null || failed is not null;
            var preparing = staged is not null && plan is null && cancel is null && !ended;
            bool valid;
            switch (fact)
            {
                case ApplicationImportStaged value:
                    valid = staged is null && revision == 0 && Scope(value.TenantId, value.BatchId) &&
                        value.SubmissionId != Uuid.Empty && value.Operation.LogicalId == value.SubmissionId &&
                        value.FirstResponse is not null && value.OperationResult is not null &&
                        value.RowCount > 0 && value.RowsManifest.Bytes >= value.RowCount &&
                        value.RowsManifest.Bytes <= ApplicationImportBatch.MaximumStagedBytes && value.InvalidCount >= 0 && value.InvalidCount <= value.RowCount &&
                        ApplicationImportBatch.BatchIdFor(new StageApplicationImport(value.TenantId, value.SubmissionId,
                            value.SourceKey, value.SourceNamespace, value.Coverage, [])) == owner.Id &&
                        Personal(operation, nameof(StageApplicationImport), null, value.ActorMemberId) &&
                        value.SubmittedAt == operation.AcceptedAt;
                    if (valid)
                        for (var index = 1; index <= value.RowCount; index++)
                            rows.Add(ApplicationImportEffects.RowId(owner.Id, index));
                    staged = value;
                    break;
                case ApplicationImportRowCorrelated value:
                    valid = preparing && Scope(value.TenantId, value.BatchId) && value.Revision == revision + 1 &&
                        value.SourceKey == staged!.SourceKey && value.SourceNamespace == staged.SourceNamespace &&
                        rows.Contains(value.RowId) && !string.IsNullOrWhiteSpace(value.SourceRecordId) &&
                        value.ApplicationId != Uuid.Empty && value.Decision is "create_new" or "link_existing" &&
                        value.FirstResponse is not null && value.OperationResult is not null &&
                        Personal(operation, nameof(CorrelateApplicationImportRow), (ulong)revision, value.ActorMemberId) &&
                        value.RecordedAt == operation.AcceptedAt;
                    if (correlations.TryGetValue(value.RowId, out var previous) && previous.SourceRecordId != value.SourceRecordId)
                        valid = false;
                    correlations[value.RowId] = value;
                    break;
                case ApplicationImportRetirementProposalSealed value:
                    valid = preparing && Scope(value.TenantId, value.BatchId) && value.Revision == revision + 1 &&
                        proposal?.Revision != revision && value.FirstResponse is not null && value.OperationResult is not null &&
                        value.ProposalSha256 == value.Proposal.Sha256 &&
                        Personal(operation, nameof(FreezeApplicationImportOmissionProposal), (ulong)revision);
                    proposal = value;
                    break;
                case ApplicationImportPlanSealed value:
                    valid = preparing && Scope(value.TenantId, value.BatchId) && value.Revision == revision + 1 &&
                        value.SourceKey == staged!.SourceKey && value.SourceNamespace == staged.SourceNamespace &&
                        value.FirstResponse is not null && value.RowCount > 0 && value.TargetGroupCount > 0 &&
                        value.PlanSha256 == value.FrozenPlan.Sha256 &&
                        Personal(operation, nameof(AcceptApplicationImport), (ulong)revision);
                    plan = value;
                    break;
                case ApplicationImportExecutionAuthorized value:
                    valid = plan is not null && authorization is null && cancel is null && !ended && value.Owner == owner.Stream &&
                        value.Revision == revision + 1 && value.Generation == ApplicationImportEffects.Generation &&
                        value.Intent == plan.FrozenPlan && Child(operation, plan.Operation, plan.Metadata.EventId,
                            nameof(ExecuteApplicationImport));
                    authorization = value;
                    break;
                case ApplicationImportCancellationAccepted value:
                    valid = staged is not null && cancel is null && !ended && value.Owner == owner.Stream &&
                        value.Revision == revision + 1 && value.Generation == ApplicationImportEffects.Generation &&
                        value.FirstResponse is not null && Personal(operation, nameof(CancelApplicationImport), (ulong)revision);
                    cancel = value;
                    break;
                case ApplicationImportCommitted value:
                    valid = plan is not null && authorization is not null && cancel is null && !ended &&
                        Scope(value.TenantId, value.BatchId) && value.Revision == revision + 1 &&
                        value.SourceKey == staged!.SourceKey && value.SourceNamespace == staged.SourceNamespace &&
                        value.PlanSha256 == plan.PlanSha256 && value.Intent == plan.FrozenPlan && value.OperationResult is not null &&
                        Child(operation, plan.Operation, authorization.Metadata.EventId, nameof(CommitApplicationImportBatch)) &&
                        value.CommittedAt != default;
                    committed = value;
                    break;
                case ApplicationImportCanceled value:
                    valid = cancel is not null && !ended && Scope(value.TenantId, value.BatchId) &&
                        value.Revision == revision + 1 && value.Intent == cancel.Intent && value.OperationResult is not null &&
                        Child(operation, cancel.Operation, cancel.Metadata.EventId, nameof(CompleteApplicationImportCancellation)) &&
                        Member(operation, value.ActorMemberId) && value.CanceledAt != default;
                    canceled = value;
                    break;
                case ApplicationImportFailed value:
                    // This retained catalog event has no selected current producer. Reading it creates no callback authority.
                    valid = plan is not null && !ended && Scope(value.TenantId, value.BatchId) && value.Revision == revision + 1 &&
                        value.SourceKey == staged!.SourceKey && value.SourceNamespace == staged.SourceNamespace &&
                        value.PlanSha256 == plan.PlanSha256 && operation.FirstOwner == owner.Stream &&
                        operation.AffectedOwner == owner.Stream && operation.ExpectedRevisionOwner == owner.Stream &&
                        operation.RootOperationId == plan.Operation.RootOperationId &&
                        (operation.CausationEventId == plan.Metadata.EventId || operation.CausationEventId == authorization?.Metadata.EventId) &&
                        (value.RowId is null || rows.Contains(value.RowId.Value)) && !string.IsNullOrWhiteSpace(value.FailureCode) &&
                        value.OperationResult is not null && operation.Actor == plan.Operation.Actor && operation.AuthoredIntentSha256 == plan.Operation.AuthoredIntentSha256;
                    failed = value;
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
                throw Invalid("The import batch prefix has another scope, revision, mutable phase or frozen native root lineage.");
            revision++;
        }
        if (owner.Revision != revision || !Same(staged, owner.Staged) || !Same(proposal, owner.Proposal) ||
            !Same(plan, owner.Plan) || !Same(authorization, owner.Authorization) || !Same(cancel, owner.CancellationAccepted) ||
            !Same(committed, owner.Committed) || !Same(canceled, owner.Canceled) || !Same(failed, owner.Failed) ||
            !Same(correlations.GetValueOrDefault(owner.SelectedRowId), owner.GetCorrelation(owner.SelectedRowId)) ||
            (owner.Latest is null) != (facts.Count == 0) || facts.Count != 0 &&
                ContentDigest.Sha256(owner.Latest!, owner.Latest!.GetType()) != ContentDigest.Sha256(facts[^1], facts[^1].GetType()))
            throw Invalid("The complete import prefix differs from compact current/selected batch authority.");
        owner.NativeCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new(owner, facts, correlations);

        bool Scope(Uuid tenant, Uuid batch) => tenant == owner.TenantId && batch == owner.Id;
        bool Personal(OperationProvenance operation, string kind, ulong? expected, Uuid? member = null) =>
            NativeOwnerHistory.IsOwnRoot(owner, operation, kind, expected) && Member(operation, member);
    }

    static bool Member(OperationProvenance operation, Uuid? expected) => operation.Actor.Kind == "member" &&
        Uuid.TryParse(operation.Actor.Id, CultureInfo.InvariantCulture, out var id) && id != Uuid.Empty &&
        (expected is null || expected == id);
    static bool Child(OperationProvenance child, OperationProvenance root, Uuid cause, string kind) =>
        child.CommandKind == kind && child.CausationEventId == cause && child.RootOperationId == root.RootOperationId &&
        child.FirstOwner == root.Owner && child.AffectedOwner == root.Owner && child.ExpectedRevisionOwner == root.Owner &&
        child.Actor == root.Actor && child.Episode == root.Episode && child.OriginalExpectedRevision == root.OriginalExpectedRevision &&
        child.AuthoredIntentSha256 == root.AuthoredIntentSha256;
    static bool Same(DomainEvent? expected, DomainEvent? actual) => expected is null ? actual is null : actual is not null &&
        expected.GetType() == actual.GetType() && expected.Equals(actual) && expected.Metadata == actual.Metadata;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
