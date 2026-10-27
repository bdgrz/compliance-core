using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Reactors;

/// <summary>
///     Routes every submitted material evaluation deviation into the shared finding path. It watches the evaluation
///     streams of the tenant and reads the verified submission content of each submitted round, so a deviation is
///     routed from the exact fact that submitted it. The finding id derives from the deviation, so replay and
///     resubmission never duplicate one.
/// </summary>
public sealed partial class ControlEvaluationDeviationFindingReactor(
    IProjectionCheckpointStore checkpoints, IRequestBus bus, IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ControlEvaluation.Area), WorkloadName),
      IReactorHandler<ControlEvaluationSubmitted>
{
    public const string WorkloadName = "ControlEvaluationDeviationFindingsV1";

    public async ValueTask HandleAsync(IReactorContext<ControlEvaluationSubmitted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context,
            new ControlEvaluation(ev.TenantId, ev.EvaluationId).Stream, ct).ConfigureAwait(false);
        EvaluationSubmissionView submission;
        try
        {
            submission = await NativeContent.ReadAsync<EvaluationSubmissionView>(content, ev.TenantId,
                ev.SubmissionContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The evaluation submission cannot be verified yet: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        foreach (var deviation in submission.Deviations.Where(static deviation =>
                     deviation.Classification == ControlEvaluationStates.Material))
            await bus.SendReactionAsync(new RaiseEvaluationDeviationFinding(ev.TenantId,
                ev.ProgramId, Uuid.CreateVersion5(deviation.DeviationId, "finding"), ev.ControlId,
                ev.EvaluationId, deviation.DeviationId), context, ct).ConfigureAwait(false);
    }
}
