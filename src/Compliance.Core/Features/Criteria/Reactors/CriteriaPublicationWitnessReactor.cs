using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Reactors;

/// <summary>Delivers publication starts, aborts and fences to their exact participant owners.</summary>
public sealed partial class CriteriaPublicationWitnessReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz"), "CriteriaPublicationWitnessV1"),
      IReactorHandler<CriteriaPublicationStarted>, IReactorHandler<CriteriaPublicationAborted>,
      IReactorHandler<CriterionWitnessPinFenced>
{
    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaPublicationStarted> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        if (context.Source.Stream != CriteriaEdition.AddressOf(fact.EditionId))
            return InvalidSource();
        return bus.SendReactionAsync(new PinCriteriaPublicationWitness(fact.EditionId, fact.PublicationAttemptId,
            fact.WitnessIdentifier, context.Source.ResourceOffset, fact.Metadata.EventId), context, ct);
    }

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaPublicationAborted> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        if (context.Source.Stream != CriteriaEdition.AddressOf(fact.EditionId))
            return InvalidSource();
        return bus.SendReactionAsync(new FenceCriteriaPublicationWitness(fact.EditionId, fact.PublicationAttemptId,
            fact.WitnessIdentifier, context.Source.ResourceOffset, fact.Metadata.EventId), context, ct);
    }

    public ValueTask<Result> HandleAsync(IReactorContext<CriterionWitnessPinFenced> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        if (context.Source.Stream != CriteriaEntry.StreamFor(fact.EditionId, fact.Identifier))
            return InvalidSource();
        return bus.SendReactionAsync(new CompleteCriteriaPublicationAbort(fact.EditionId, fact.PublicationAttemptId,
            fact.Identifier, context.Source.ResourceOffset, fact.Metadata.EventId), context, ct);
    }

    static ValueTask<Result> InvalidSource() => ValueTask.FromResult(Result.Failure(
        new RequestError(RequestErrorKind.Validation, "The publication fact came from an unexpected source stream.")));
}
