using System.Globalization;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Routes control-operation exceptions into the shared finding path: a failed or skipped
///     attestation and each action a reviewer requests become a finding whose identity derives
///     from the exact attestation or decision, so replay never duplicates one. It reads the occurrence facts of the
///     tenant and their verified attestation and review content; the finding ids derive from those exact ids.
/// </summary>
public sealed partial class ControlOccurrenceFindingReactor(IProjectionCheckpointStore checkpoints,
    IRequestBus bus, IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ControlOccurrence.Area), WorkloadName),
      IReactorHandler<ControlOccurrenceAttested>, IReactorHandler<ControlOccurrenceReviewed>
{
    public const string WorkloadName = "ControlOccurrenceFindingsV1";

    public async ValueTask HandleAsync(IReactorContext<ControlOccurrenceAttested> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context,
            new ControlOccurrence(ev.TenantId, ev.ProgramId, ev.ControlId, ev.OccurrenceId).Stream, ct).ConfigureAwait(false);
        var attestation = await ReadAsync<ControlAttestationView>(context, ev.AttestationContent, ct)
            .ConfigureAwait(false);
        if (attestation.Result is not (ControlOperationStates.Failed or ControlOperationStates.Skipped))
            return;
        await bus.SendReactionAsync(new RaiseOccurrenceFinding(ev.TenantId, ev.ProgramId,
            Uuid.CreateVersion5(attestation.AttestationId, "finding"), ev.ControlId,
            ev.OccurrenceId, attestation.AttestationId.ToString(), "control_occurrence",
            $"{attestation.Result}: {attestation.Rationale}"), context, ct).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(IReactorContext<ControlOccurrenceReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context,
            new ControlOccurrence(ev.TenantId, ev.ProgramId, ev.ControlId, ev.OccurrenceId).Stream, ct).ConfigureAwait(false);
        var review = await ReadAsync<ControlOccurrenceReviewView>(context, ev.ReviewContent, ct)
            .ConfigureAwait(false);
        if (review.Outcome != ControlOperationStates.ActionRequested)
            return;
        for (var index = 0; index < review.RequestedActions.Count; index++)
            await bus.SendReactionAsync(new RaiseOccurrenceFinding(ev.TenantId, ev.ProgramId,
                Uuid.CreateVersion5(review.DecisionId, "requested-action-" +
                    index.ToString(CultureInfo.InvariantCulture)), ev.ControlId, ev.OccurrenceId,
                review.DecisionId.ToString(), "occurrence_review", review.RequestedActions[index]),
                context, ct).ConfigureAwait(false);
    }

    async ValueTask<T> ReadAsync<T>(IReactorContext context, ImmutableContentRef reference, CancellationToken ct)
        where T : notnull
    {
        try
        {
            return await content.ReadJsonAsync<T>(context.Source.Stream.Realm, reference, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The occurrence content cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
    }
}
