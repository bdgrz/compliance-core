using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Reactors;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

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
