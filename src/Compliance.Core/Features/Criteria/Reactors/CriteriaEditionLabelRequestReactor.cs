using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Reactors;

/// <summary>Dispatches each committed edition label request to the corresponding label owner.</summary>
public sealed partial class CriteriaEditionLabelRequestReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz"), "CriteriaEditionLabelRequestV1"),
      IReactorHandler<CriteriaEditionDraftRequested>, IReactorHandler<CriteriaInitialLabelRequested>,
      IReactorHandler<CriteriaEditionRenameRequested>
{
    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionDraftRequested> context, CancellationToken ct) =>
        SendAsync(context, context.Trigger.EditionId, context.Trigger.OperationId, context.Trigger.Label, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaInitialLabelRequested> context, CancellationToken ct) =>
        SendAsync(context, context.Trigger.EditionId, context.Trigger.OperationId, context.Trigger.Label, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionRenameRequested> context, CancellationToken ct) =>
        SendAsync(context, context.Trigger.EditionId, context.Trigger.OperationId, context.Trigger.NewLabel, ct);

    ValueTask<Result> SendAsync<TEvent>(IReactorContext<TEvent> context, Uuid editionId, Uuid operationId,
        string label, CancellationToken ct) where TEvent : DomainEvent
    {
        if (context.Source.Stream != CriteriaEdition.AddressOf(editionId))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The edition request came from an unexpected source stream.")));

        return bus.SendReactionAsync(new ReserveCriteriaEditionLabel(editionId, operationId, label,
            context.Source.ResourceOffset, context.Trigger.Metadata.EventId), context, ct);
    }
}
