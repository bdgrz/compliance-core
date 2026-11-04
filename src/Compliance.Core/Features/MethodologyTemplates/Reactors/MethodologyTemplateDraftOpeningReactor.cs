using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;

/// <summary>Opens the separate draft after the template line commits its start.</summary>
public sealed partial class MethodologyTemplateDraftOpeningReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(MethodologyTemplate.Realm, MethodologyTemplate.Area), WorkloadName),
        IReactorHandler<MethodologyTemplateDraftStarted>
{
    public const string WorkloadName = "MethodologyTemplateDraftOpeningV1";

    public ValueTask<Result> HandleAsync(IReactorContext<MethodologyTemplateDraftStarted> context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"The line start for draft {context.Trigger.DraftId} has not been dispatched.", true)));
    }
}
