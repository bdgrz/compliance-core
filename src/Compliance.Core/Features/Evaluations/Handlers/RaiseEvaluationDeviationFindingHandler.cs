using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
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
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Reactors;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evaluations.Handlers;

/// <summary>
///     Raises the owned finding and corrective action for a submitted material deviation. The
///     control version's owner member (or the evaluator when the owner does not sign in) owns it,
///     due in 30 days at medium severity, matching the occurrence-finding product default.
/// </summary>
public sealed class RaiseEvaluationDeviationFindingHandler(NativeOperationExecutor operations,
    IAggregateReader reader, ControlReader controlSources, IImmutableContentStore content, ControlEvaluationReader evaluations, TimeProvider clock)
    : IRequestHandler<RaiseEvaluationDeviationFinding>
{
    public const string ProcessId = "reactor:" + ControlEvaluationDeviationFindingReactor.WorkloadName;

    public ValueTask<Result> HandleAsync(
        IRequestContext<RaiseEvaluationDeviationFinding> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result> HandleCoreAsync(
        IRequestContext<RaiseEvaluationDeviationFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        var actor = ActorReference.ForSystemProcess(ProcessId,
            ControlEvaluationDeviationFindingReactor.WorkloadName);
        async ValueTask<Result<RoutedFindingSource>> PrepareSourceAsync(CancellationToken token)
        {
            var evaluation = await evaluations.ReadViewAsync(request.TenantId, request.ProgramId, request.ControlId,
                request.EvaluationId, token).ConfigureAwait(false);
            var deviation = evaluation?.Submissions.SelectMany(static submission => submission.Deviations)
                .LastOrDefault(item => item.DeviationId == request.DeviationId);
            if (evaluation is null || deviation is not { Classification: ControlEvaluationStates.Material } ||
                deviation.FindingId != request.FindingId)
                return Result<RoutedFindingSource>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The submitted material deviation was not found."));
            var control = await controlSources.ReadAsync(request.TenantId, request.ProgramId, request.ControlId, token)
                .ConfigureAwait(false);
            var owner = control.ReadVersions().FirstOrDefault(version =>
                version.VersionId == evaluation.ControlVersionId)?.OwnerMemberId is { } member && member != Uuid.Empty
                ? member
                : evaluation.EvaluatorMemberId;
            return Result<RoutedFindingSource>.Success(new RoutedFindingSource(
                new FindingSource("evaluation_deviation", deviation.DeviationId, request.EvaluationId.ToString(),
                    deviation.Description), "Material control evaluation deviation",
                $"Control {request.ControlId}, evaluation {request.EvaluationId}", owner,
                [new FindingLink("control", request.ControlId.ToString()),
                    new FindingLink("control_evaluation", request.EvaluationId.ToString())], deviation.Description));
        }
        return await RoutedFinding.RaiseFromSourceAsync(operations, reader, content, context,
            "RaiseEvaluationDeviationFinding", request, request.TenantId, request.ProgramId, request.FindingId,
            actor, now, PrepareSourceAsync, ct).ConfigureAwait(false);
    }
}
