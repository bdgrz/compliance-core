using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

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
