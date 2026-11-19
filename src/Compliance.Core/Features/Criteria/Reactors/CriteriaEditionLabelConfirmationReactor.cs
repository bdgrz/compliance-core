using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Reactors;

/// <summary>Reconciles committed edition activations and renames with their label owners.</summary>
public sealed partial class CriteriaEditionLabelConfirmationReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz"), "CriteriaEditionLabelConfirmationV1"),
      IReactorHandler<CriteriaEditionDraftActivated>,
      IReactorHandler<CriteriaEditionDraftRenamed>
{
    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionDraftActivated> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        if (context.Source.Stream != CriteriaEdition.AddressOf(fact.EditionId))
            return InvalidSource();

        return bus.SendReactionAsync(new ConfirmCriteriaEditionLabel(fact.EditionId, fact.OperationId, fact.Label,
            fact.LabelGeneration, fact.ReservationEventId, context.Source.ResourceOffset, fact.Metadata.EventId),
            context, ct);
    }

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionDraftRenamed> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        if (context.Source.Stream != CriteriaEdition.AddressOf(fact.EditionId))
            return InvalidSource();

        return bus.SendReactionAsync(new ConfirmCriteriaEditionLabel(fact.EditionId, fact.OperationId, fact.NewLabel,
            fact.NewLabelGeneration, fact.ReservationEventId, context.Source.ResourceOffset, fact.Metadata.EventId),
            context, ct);
    }

    static ValueTask<Result> InvalidSource() => ValueTask.FromResult(Result.Failure(
        new RequestError(RequestErrorKind.Validation, "The edition fact came from an unexpected source stream.")));
}
