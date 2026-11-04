using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

public sealed partial class MethodologyTemplateDraftConclusionReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(MethodologyTemplate.Realm, MethodologyTemplateDraft.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateDraftApproved>, IReactorHandler<MethodologyTemplateDraftDiscarded>
{
    public const string WorkloadName = "MethodologyTemplateDraftConclusionV1";
    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateDraftApproved> context, CancellationToken ct) =>
        Refuse(context);
    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateDraftDiscarded> context, CancellationToken ct) =>
        Refuse(context);
    ValueTask<Result> Refuse(IReactorContext context)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Draft terminal fact {context.Source.Event.Metadata.EventId} has no line reaction yet.")));
    }
}
