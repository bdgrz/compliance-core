using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records an auditor's selection against its exact request and frozen population version.</summary>
public sealed class RecordSampleSelectionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<RecordSampleSelection>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordSampleSelection> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recording a sample selection requires a personal HTTP actor."));

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
                var failure = aggregate.RecordSampleSelection(request.ExpectedRevision,
                    request.SelectionId, request.PopulationId, request.PopulationVersion,
                    population.Value.ContentSha256, request.SelectionMethod,
                    request.PopulationRowIds, request.Source, request.ExternalReference,
                    actorMemberId, actorDisplay, clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure);
            }, context, ct).ConfigureAwait(false);
    }
}
