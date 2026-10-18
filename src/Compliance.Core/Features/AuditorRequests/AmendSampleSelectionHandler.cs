using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Appends an exact-version sample selection linked to the selection it supersedes.</summary>
public sealed class AmendSampleSelectionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<AmendSampleSelection>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AmendSampleSelection> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Amending a sample selection requires a personal HTTP actor."));

        var population = await SampleSelectionPopulationSourceReader.LoadAsync(reader,
            request.TenantId, request.ProgramId, request.ExaminationId, request.PopulationId,
            request.PopulationVersion, request.PopulationRowIds, ct).ConfigureAwait(false);
        if (!population.IsSuccess)
            return Result.Failure(population.Error);

        return await executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.AuditorRequestId), aggregate =>
            {
                if (aggregate.Current is not { } current)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The auditor request was not found.")));
                if (current.ExaminationId != request.ExaminationId)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The auditor request belongs to a different examination.")));
                var failure = aggregate.AmendSampleSelection(request.ExpectedRevision,
                    request.SelectionId, request.SupersedesSelectionId, request.PopulationId,
                    request.PopulationVersion, population.Value.ContentSha256,
                    request.SelectionMethod, request.PopulationRowIds, request.Source,
                    request.ExternalReference, request.AmendmentReason, actorMemberId,
                    actorDisplay, clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure);
            }, context, ct).ConfigureAwait(false);
    }
}
