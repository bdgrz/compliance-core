using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evaluations.Services;

/// <summary>Folds the facts of one evaluation stream into its working state, reading each fact's immutable content.</summary>
static class ControlEvaluationFold
{
    /// <summary>The state after every fact in order, or null when the stream holds no start.</summary>
    public static async Task<ControlEvaluationState?> ReadAsync(IEnumerable<DomainEvent> facts,
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ControlEvaluationState? state = null;
        foreach (var fact in facts)
            state = await ApplyAsync(state, fact, content, tenantId, ct).ConfigureAwait(false);
        return state;
    }

    /// <summary>Folds one fact into the state; a start creates it.</summary>
    public static async Task<ControlEvaluationState?> ApplyAsync(ControlEvaluationState? state, DomainEvent fact,
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        switch (fact)
        {
            case ControlEvaluationStarted started:
                var steps = await NativeContent.ReadAsync<IReadOnlyList<EvaluationProcedureStep>>(content, tenantId,
                    started.StepsManifest, ct).ConfigureAwait(false);
                var retested = await NativeContent.ReadAsync<IReadOnlyList<Uuid>>(content, tenantId,
                    started.RetestOfDeviationIdsManifest, ct).ConfigureAwait(false);
                return new ControlEvaluationState(started.TenantId, started.ProgramId, started.ControlId,
                    started.EvaluationId, started.ControlVersionId, started.PlanVersionId, started.PlanVersion, steps,
                    started.EvaluatorMemberId, started.StartedBy, started.StartedAt, started.RetestOfEvaluationId,
                    retested, started.RetestPredecessor, started.Revision);
            case ControlEvaluationStepRecorded recorded:
                var current = Require(state);
                current.RecordStep(recorded.Revision, recorded.StepId,
                    await NativeContent.ReadAsync<EvaluationStepResultView>(content, tenantId, recorded.ResultContent, ct)
                        .ConfigureAwait(false),
                    recorded.DeviationContent is { } deviationContent
                        ? await NativeContent.ReadAsync<EvaluationDeviationView>(content, tenantId, deviationContent, ct)
                            .ConfigureAwait(false)
                        : null);
                return current;
            case ControlEvaluationDeviationDisposed disposed:
                current = Require(state);
                current.DisposeDeviation(disposed.Revision, disposed.DeviationId, disposed.Disposition,
                    disposed.Rationale, disposed.WaiverId);
                return current;
            case ControlEvaluationSubmitted submitted:
                current = Require(state);
                current.Submit(submitted.Revision, await NativeContent.ReadAsync<EvaluationSubmissionView>(content,
                    tenantId, submitted.SubmissionContent, ct).ConfigureAwait(false));
                return current;
            case ControlEvaluationReviewed reviewed:
                current = Require(state);
                current.Review(reviewed.Revision, await NativeContent.ReadAsync<ControlEvaluationReviewView>(content,
                    tenantId, reviewed.ReviewContent, ct).ConfigureAwait(false));
                if (reviewed.Decision == ControlEvaluationStates.Accepted)
                    current.AcceptingFact = (reviewed.Metadata!.EventId, reviewed.Metadata.AggregateVersion);
                return current;
            default:
                throw new ArgumentException("The fact is not a control evaluation fact.", nameof(fact));
        }
    }

    static ControlEvaluationState Require(ControlEvaluationState? state) =>
        state ?? throw new InvalidOperationException("An evaluation fact cannot precede its start.");
}
