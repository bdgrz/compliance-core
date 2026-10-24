using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Routes every submitted material evaluation deviation into the shared finding path. It watches the evaluation
///     streams of the tenant and reads the verified submission content of each submitted round, so a deviation is
///     routed from the exact fact that submitted it. The finding id derives from the deviation, so replay and
///     resubmission never duplicate one.
/// </summary>
public sealed partial class ControlEvaluationDeviationFindingReactor(
    IProjectionCheckpointStore checkpoints, IRequestBus bus, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ControlEvaluation.Area), WorkloadName),
      IReactorHandler<ControlEvaluationSubmitted>
{
    public const string WorkloadName = "ControlEvaluationDeviationFindingsV1";

    public async ValueTask HandleAsync(IReactorContext<ControlEvaluationSubmitted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        EvaluationSubmissionView submission;
        try
        {
            submission = await NativeContent.ReadAsync<EvaluationSubmissionView>(content, ev.TenantId,
                ev.SubmissionContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The evaluation submission cannot be verified yet: {exception.Message}", true));
        }

        foreach (var deviation in submission.Deviations.Where(static deviation =>
                     deviation.Classification == ControlEvaluationStates.Material))
            await bus.SendReactionAsync(new RaiseEvaluationDeviationFinding(ev.TenantId,
                ev.ProgramId, Uuid.CreateVersion5(deviation.DeviationId, "finding"), ev.ControlId,
                ev.EvaluationId, deviation.DeviationId), context, ct).ConfigureAwait(false);
    }
}
