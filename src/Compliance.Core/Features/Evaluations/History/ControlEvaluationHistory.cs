using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evaluations.History;

/// <summary>Verifies complete evaluation history outside compact command authority at its exact native cut.</summary>
public static class ControlEvaluationHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events,
        ControlEvaluation owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var operations = new HashSet<Uuid>();
        var digest = EvaluationReplayDigest.Empty;
        var lifecycle = string.Empty;
        long revision = 0;
        var selectedSeen = false;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || !operations.Add(native.Operation.LogicalId) ||
                native.FirstResponse is null ||
                native.Operation.Actor is not { Kind: "member" } actor ||
                !Uuid.TryParse(actor.Id, CultureInfo.InvariantCulture, out var actorId) || actorId == Uuid.Empty)
                throw Invalid("The evaluation history repeats an operation or has no original member/result binding.");
            digest = EvaluationReplayDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId)
            {
                selectedSeen = true;
                if (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata)
                    throw Invalid("The selected evaluation fact differs from its complete original payload and metadata.");
            }
            bool valid;
            switch (fact)
            {
                case ControlEvaluationStarted started:
                    valid = revision == 0 && Scope(started.TenantId, started.ProgramId, started.ControlId,
                        started.EvaluationId, started.Revision) && started.Operation.LogicalId == owner.Id &&
                        started.EvaluatorMemberId == actorId && started.StartedBy == native.Operation.Actor &&
                        started.StartedAt == native.Operation.AcceptedAt &&
                        NativeOwnerHistory.IsOwnRoot(owner, native.Operation, nameof(StartControlEvaluation), null);
                    lifecycle = ControlEvaluationStates.InProgress;
                    break;
                case ControlEvaluationStepRecorded step:
                    valid = Scope(step.TenantId, step.ProgramId, step.ControlId, step.EvaluationId, step.Revision) &&
                        Work(nameof(RecordControlEvaluationStep));
                    break;
                case ControlEvaluationDeviationDisposed disposition:
                    valid = Scope(disposition.TenantId, disposition.ProgramId, disposition.ControlId,
                        disposition.EvaluationId, disposition.Revision) && Work(nameof(DisposeControlEvaluationDeviation));
                    break;
                case ControlEvaluationSubmitted submission:
                    valid = Scope(submission.TenantId, submission.ProgramId, submission.ControlId,
                        submission.EvaluationId, submission.Revision) && Work(nameof(SubmitControlEvaluation));
                    lifecycle = ControlEvaluationStates.Submitted;
                    break;
                case ControlEvaluationReviewed review:
                    valid = Scope(review.TenantId, review.ProgramId, review.ControlId, review.EvaluationId,
                        review.Revision) && lifecycle == ControlEvaluationStates.Submitted &&
                        review.Decision is "accepted" or "rejected" or "changes_requested" &&
                        NativeOwnerHistory.IsOwnRoot(owner, native.Operation, nameof(ReviewControlEvaluation), (ulong)revision);
                    lifecycle = review.Decision == ControlEvaluationStates.Accepted
                        ? ControlEvaluationStates.Accepted : ControlEvaluationStates.InProgress;
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
                throw Invalid("The evaluation history has another scope, lifecycle, revision or original root binding.");
            revision++;

            bool Work(string command) => lifecycle == ControlEvaluationStates.InProgress &&
                NativeOwnerHistory.IsOwnRoot(owner, native.Operation, command, (ulong)revision);
            bool Scope(Uuid tenant, Uuid program, Uuid control, Uuid evaluation, long next) =>
                tenant == owner.TenantId && program == owner.ProgramId && control == owner.ControlId &&
                evaluation == owner.Id && next == revision + 1;
        }
        if (revision != owner.Revision || lifecycle != owner.Lifecycle ||
            selectedSeen != (owner.Acceptance.SelectedFact is not null) ||
            (facts.Count == 0) != (owner.Latest is null))
            throw Invalid("The complete evaluation history differs from compact command authority.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return facts;
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
